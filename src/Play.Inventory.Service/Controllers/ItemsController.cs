using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Play.Common;
using Play.Inventory.Contracts;
using Play.Inventory.Service.Dtos;
using Play.Inventory.Service.Entities;
using System.IdentityModel.Tokens.Jwt;

namespace Play.Inventory.Service.Controllers
{

    [ApiController]
    [Route("items")]
    public class ItemsController : ControllerBase
    {
        private const string AdminRole = "Admin";
        private readonly IRepository<InventoryItem> itemsRepository;
        private readonly IRepository<CatalogItem> catalogItemsRepository;
        private readonly IPublishEndpoint publishEndpoint;

        public ItemsController(IRepository<InventoryItem> itemsRepository, 
        IRepository<CatalogItem> catalogItemsRepository, 
        IPublishEndpoint publishEndpoint)
        {
            this.itemsRepository = itemsRepository;
            this.catalogItemsRepository = catalogItemsRepository;
            this.publishEndpoint = publishEndpoint;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest();
            }

            var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (Guid.Parse(currentUserId) != userId)
            {
                if (!User.IsInRole(AdminRole))
                {
                    return Forbid();
                }
            }

            var inventoryItemsEntities = await itemsRepository.GetAllAsync(item => item.UserId == userId);
            var itemIds = inventoryItemsEntities.Select(item => item.CatalogItemId);
            var catalogItemsEntities = await catalogItemsRepository.GetAllAsync(catalogItem => itemIds.Contains(catalogItem.Id));

            var inventoryItemsDto = inventoryItemsEntities.Select(item =>
            {
                var catalogItem = catalogItemsEntities.Single(catalogItem => catalogItem.Id == item.CatalogItemId);
                return item.AsDto(catalogItem.Name, catalogItem.Description);
            });

            return Ok(inventoryItemsDto);
        }

        [HttpPost]
        [Authorize(Roles = AdminRole)]
        public async Task<ActionResult> PostAsync(GrantItemsDto grantItemsDto)
        {
            var inventoryItem = await itemsRepository.GetAsync(
                item => item.UserId == grantItemsDto.UserId && item.CatalogItemId == grantItemsDto.CatalogItemId);

            if (inventoryItem == null)
            {
                inventoryItem = new InventoryItem
                {
                    CatalogItemId = grantItemsDto.CatalogItemId,
                    UserId = grantItemsDto.UserId,
                    Quantity = grantItemsDto.Quantity,
                    AcquiredDate = DateTimeOffset.UtcNow
                };

                await itemsRepository.CreateAsync(inventoryItem);
            }
            else
            {
                inventoryItem.Quantity += grantItemsDto.Quantity;
                await itemsRepository.UpdateAsync(inventoryItem);
            }

            await publishEndpoint.Publish(new InventoryItemUpdated(
                inventoryItem.UserId, 
                inventoryItem.CatalogItemId, 
                inventoryItem.Quantity));
            
            return Ok();
        }
    }
}