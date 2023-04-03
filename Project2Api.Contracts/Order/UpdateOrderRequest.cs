namespace Project2Api.Contracts.Order
{
    public record UpdateOrderRequest(
        DateTime dateTime,
        List<string> Items, 
        float Price
    );
}