using System;
using System.Threading.Tasks;
using MassTransit;
using Play.Common;
using Play.Inventory.Contracts;
using Play.Inventory.Service.Entities;
using Play.Inventory.Service.Exceptions;

namespace Play.Inventory.Service.Consumers;

public class GrantItemsConsumer : IConsumer<GrantItems>
{
    private readonly IRepository<InventoryItem> inventoryItemsRepository;
    private readonly IRepository<CatalogItem> catalogItemsRepository;

    public GrantItemsConsumer(IRepository<InventoryItem> itemsRepository, IRepository<CatalogItem> catalogItemsRepository)
    {
        this.inventoryItemsRepository = itemsRepository;
        this.catalogItemsRepository = catalogItemsRepository;
    }

    public async Task Consume(ConsumeContext<GrantItems> context)
    {
        var message = context.Message;
        var item = await catalogItemsRepository.GetAsync(message.CatalogItemId)
        ?? throw new UnknowItemException(message.CatalogItemId);

        var inventoryItem = await inventoryItemsRepository.GetAsync(
                        item => item.UserId == message.UserId && item.CatalogItemId == message.CatalogItemId);

        if (inventoryItem == null)
        {
            inventoryItem = new InventoryItem
            {
                CatalogItemId = message.CatalogItemId,
                UserId = message.UserId,
                Quantity = message.Quantity,
                AcquiredDate = DateTimeOffset.UtcNow
            };

            await inventoryItemsRepository.CreateAsync(inventoryItem);
        }
        else
        {
            inventoryItem.Quantity += message.Quantity;
            await inventoryItemsRepository.UpdateAsync(inventoryItem);
        }

        await context.Publish(new InventoryItemsGranted(message.CorrelationId));
    }
}
