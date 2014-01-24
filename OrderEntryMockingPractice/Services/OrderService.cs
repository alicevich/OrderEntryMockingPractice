using System;
using System.Linq;
using OrderEntryMockingPractice.Models;

namespace OrderEntryMockingPractice.Services
{
    public class OrderService
    {
        private readonly IProductRepository _productRepository;
        private readonly IOrderFulfillmentService _orderFulfillmentService;
        private readonly ITaxRateService _taxRateService;

        public OrderService(IProductRepository productRepository, IOrderFulfillmentService orderFulfillmentService, ITaxRateService taxRateService)
        {
            _productRepository = productRepository;
            _orderFulfillmentService = orderFulfillmentService;
            _taxRateService = taxRateService;
        }

        public OrderSummary PlaceOrder(Order order)
        {
            AssertIsValidOrder(order);
            var confirmation = _orderFulfillmentService.Fulfill(order);
            var taxEntries = _taxRateService.GetTaxEntries()

            return new OrderSummary()
            {
                OrderNumber = confirmation.OrderNumber,
                OrderId = confirmation.OrderId
            };
        }

        private void AssertIsValidOrder(Order order)
        {
            if (!order.OrderItemsAreUniqueByProduct())
                throw new ValidationFailedException("Order Items are not unique by product.");

            if (AnyProductsAreNotInStock(order))
            {
                throw new ValidationFailedException("Some products are out of stock.");
            }
        }

        private bool AnyProductsAreNotInStock(Order order)
        {
            return order.OrderItems.Any(item => !_productRepository.IsInStock(item.Product.Sku));
        }
    }
}
