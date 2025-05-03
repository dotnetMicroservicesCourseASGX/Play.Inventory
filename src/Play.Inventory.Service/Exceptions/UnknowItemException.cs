using System;

namespace Play.Inventory.Service.Exceptions;

[Serializable]
internal class UnknowItemException : Exception
{
    private Guid ItemId { get; }

    public UnknowItemException(Guid itemId)
    : base($"Unkown item {itemId}")
    {
        ItemId = itemId;
    }
}