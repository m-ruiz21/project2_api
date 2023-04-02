using Project2Api.Models;
using Project2Api.DbTools;
using ErrorOr;
using Project2Api.ServiceErrors;
using System.Data;
using Microsoft.AspNetCore.Mvc;

namespace Project2Api.Services.Orders;

public class OrdersService : IOrdersService
{
    private readonly IDbClient _dbClient;
    
    public OrdersService(IDbClient dbClient)
    {
        _dbClient = dbClient;
    }

    public Order? ConvertDataTableToOrder(DataTable orderTable)
    {
        // check if table is empty
        DataRow row = orderTable.Rows[0];

        // check if any of the columns are null 
        if (row["id"] == null || row["order_time"] == null || row["price"] == null || row["items"] == null)
        {
            return null;
        }

        // convert rest of row to order
        String RawId = row["id"].ToString() ?? "";
        String? rawOrderTime = row["order_time"].ToString() ?? "";
        String? rawPrice = row["price"].ToString() ?? "";

        // create order
        Order order = new Order(Guid.Parse(RawId),
                                DateTime.Parse(rawOrderTime),
                                new List<string>(),
                                float.Parse(rawPrice)
                            );

        return order;
    }

    public ErrorOr<Order> CreateOrder(Order order)
    {
        // create order
        Task<int> orderTask = _dbClient.ExecuteNonQueryAsync(
            $"INSERT INTO orders (id, date_time, total_price) VALUES ('{order.Id}', '{order.OrderTime}', '{order.Price}')"
        );

        // check that orderTask was successful
        if (orderTask.Result == 0)
        {
            return Errors.Orders.UnexpectedError;
        }

        // add items to ordered_menu_items table
        foreach (string item in order.Items)
        {
            Task<int> itemTask = _dbClient.ExecuteNonQueryAsync(
                $"INSERT INTO ordered_menu_items (order_id, menu_item_name) VALUES ('{order.Id}', '{item}')"
            );

            // reduce stock of menu item
            Task<int> reduceMenuItemTask = _dbClient.ExecuteNonQueryAsync(
                $"UPDATE menu_items SET quantity = quantity - 1 WHERE name = '{item}'"
            );

            // reduce stock of cutlery used by menu item 
            Task<int> reduceCutleryTask = _dbClient.ExecuteNonQueryAsync(
                $"UPDATE cutlery SET quantity = cutlery.quantity - mc.quantity FROM menu_item mi JOIN menu_item_cutlery mc ON mi.name = mc.menu_item_name WHERE cutlery.name = mc.cutlery_name AND mi.name = '{item}';"
            );

            // check that itemTask and reduceStockTask was successful
            if (itemTask.Result == 0 || reduceMenuItemTask.Result == 0)
            {
                return Errors.Orders.UnexpectedError;
            }
        }

        return order;
    }

    public ErrorOr<Order> GetOrder(Guid id)
    {
        // get order from database
        Task<DataTable> orderTask = _dbClient.ExecuteQueryAsync(
            $"SELECT * FROM orders WHERE id = '{id}'"
        );
        
        // check that orderTask was successful
        DataTable orderTable = orderTask.Result; 
        if (orderTable.Rows.Count == 0)
        {
            return Errors.Orders.NotFound;
        }

        Order? order = ConvertDataTableToOrder(orderTable);

        // check that order was successfully converted 
        if (order == null)
        {
            return Errors.Orders.UnexpectedError;
        }

        // populate order.items table by getting all menu items from ordered_menu_items table
        Task<DataTable> itemsTask = _dbClient.ExecuteQueryAsync(
            $"SELECT menu_item FROM ordered_menu_items WHERE order_id = '{id}'"
        );

        // check that itemsTask was successful
        DataTable itemsTable = itemsTask.Result;
        if (itemsTable.Rows.Count == 0)
        {
            return Errors.Orders.UnexpectedError;
        }

        // insert items into order.items
        foreach (DataRow row in itemsTable.Rows)
        {
            // make sure menu_item exists
            if (row["menu_item_name"] == null)
            {
                return Errors.Orders.UnexpectedError;
            }

            string menuItem = row["menu_item_name"].ToString() ?? "";
            order.Items.Add(menuItem);
        }

        return order;   
    }

    public ErrorOr<List<Order>> GetAllOrders()
    {
        // get all orders from database
        Task<DataTable> ordersTask = _dbClient.ExecuteQueryAsync(
            $"SELECT * FROM orders"
        );

        // check that ordersTask was successful
        DataTable ordersTable = ordersTask.Result;
        if (ordersTable.Rows.Count == 0)
        {
            return Errors.Orders.UnexpectedError;
        }

        // convert ordersTable to list of orders
        List<Order> orders = new List<Order>();

        // convert each row to order
        foreach (DataRow row in ordersTable.Rows)
        {
            Order? order = ConvertDataTableToOrder(ordersTable);

            if (order == null)
            {
                return Errors.Orders.UnexpectedError;
            }

            orders.Add(order);
        }

        return orders;
    }

    public ErrorOr<Order> UpdateOrder(Guid id, Order order)
    {
        // update order
        Task<int> updateTask = _dbClient.ExecuteNonQueryAsync(
            $"UPDATE orders SET date_time = '{order.OrderTime}', total_price = '{order.Price}' WHERE id = '{id}'"
        );

        // delete all ordered menu items for this order
        Task<int> deleteTask = _dbClient.ExecuteNonQueryAsync(
            $"DELETE FROM ordered_menu_items WHERE order_id = '{id}'"
        );

        // make sure updateTask and deleteTask were successful
        if (updateTask.Result == 0 || deleteTask.Result == 0)
        {
            return Errors.Orders.UnexpectedError;
        }
        
        // add new ordered menu items for this order
        foreach (string item in order.Items)
        {
            Task<int> itemTask = _dbClient.ExecuteNonQueryAsync(
                $"INSERT INTO ordered_menu_items (order_id, menu_item_name) VALUES ('{order.Id}', '{item}')"
            );

            // make sure itemTask was successful
            if (itemTask.Result == 0)
            {
                return Errors.Orders.UnexpectedError;
            }
        }

        return order;
    }

    public ErrorOr<IActionResult> DeleteOrder(Guid id)
    {
        // delete order
        Task<int> deleteTask = _dbClient.ExecuteNonQueryAsync(
            $"DELETE FROM orders WHERE id = '{id}'"
        );

        // check that orderTask was successful
        if (deleteTask.Result == 0)
        {
            return Errors.Orders.NotFound;
        }

        return new NoContentResult();
    }
}