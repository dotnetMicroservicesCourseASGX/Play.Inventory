using System;

namespace Play.Inventory.Service.Exceptions;

[Serializable]
internal class UnknownItemException : Exception
{
    private Guid ItemId { get; }

    public UnknownItemException(Guid itemId)
    : base($"Unkown item {itemId}")
    {
        ItemId = itemId;
    }
}