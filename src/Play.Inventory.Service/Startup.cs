using System;
using System.Net.Http;
using Amazon.Runtime.Internal.Util;
using GreenPipes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Play.Common.HealthChecks;
using Play.Common.Identity;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Play.Inventory.Service.Exceptions;
using Polly;
using Polly.Timeout;

namespace Play.Inventory.Service
{
    public class Startup
    {
        private const string AllowedOriginSetting = "AllowedOrigin";
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddMongo()
            .AddMongoRepository<InventoryItem>("inventoryitems")
            .AddMongoRepository<CatalogItem>("catalogitems")
            .AddMassTransitWithMessageBroker(Configuration, retryConfigurator =>
            {
                retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));

                // mass transit will not retry when UnknowItemException is thrown, 
                // it will consume the message and move it to the error queue
                retryConfigurator.Ignore(typeof(UnknownItemException));
            })
            .AddJwtBearerAuthentication();

            // AddCatalogClient(services);

            services.AddControllers(options =>
            {
                options.SuppressAsyncSuffixInActionNames = false;
            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Inventory.Service", Version = "v1" });
            });

            services.AddHealthChecks().AddMongoDb();
        }

        private static void AddCatalogClient(IServiceCollection services)
        {
            services.AddHttpClient<CatalogClient>(client =>
            {
                client.BaseAddress = new Uri("https://localhost:5001");
            })
            .AddTransientHttpErrorPolicy(builder => builder.Or<TimeoutRejectedException>().WaitAndRetryAsync(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var serviceProvider = services.BuildServiceProvider();
                    serviceProvider.GetService<ILogger<CatalogClient>>()
                    .LogWarning($"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryCount}.");
                }
                ))
                .AddTransientHttpErrorPolicy(builder => builder.Or<TimeoutRejectedException>().CircuitBreakerAsync(
                    3,
                    TimeSpan.FromSeconds(15),
                    onBreak: (outcome, timespan) =>
                    {
                        var serviceProvider = services.BuildServiceProvider();
                        serviceProvider.GetService<ILogger<CatalogClient>>()
                        .LogWarning($"Opening the circuit for {timespan.TotalSeconds} seconds.");
                    },
                    onReset: () =>
                    {
                        var serviceProvider = services.BuildServiceProvider();
                        serviceProvider.GetService<ILogger<CatalogClient>>()
                        .LogWarning($"Closing the circuit.");
                    }))
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Inventory.Service v1"));
                app.UseCors(builder =>
                {
                    builder.WithOrigins(Configuration[AllowedOriginSetting])
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });

            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseCookiePolicy(new CookiePolicyOptions
            {
                MinimumSameSitePolicy = SameSiteMode.Lax
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapPlayEcnomyHealthChecks();
            });
        }
    }
}
