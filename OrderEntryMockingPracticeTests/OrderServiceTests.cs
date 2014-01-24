using System;
using System.Linq;
using FizzWare.NBuilder;
using NUnit.Framework;
using OrderEntryMockingPractice.Models;
using OrderEntryMockingPractice.Services;
using Rhino.Mocks;


namespace OrderEntryMockingPracticeTests
{
    [TestFixture]
    public class OrderServiceTests
    {
        [SetUp]
        public void BeforeEachTestRuns()
        {
            this.ProductRepository = MockRepository.GenerateStub<IProductRepository>();
            this.OrderFulfillmentService = MockRepository.GenerateStub<IOrderFulfillmentService>();
            this.TaxRateService = MockRepository.GenerateStub<ITaxRateService>();
            this.OrderService = new OrderService(this.ProductRepository, this.OrderFulfillmentService, this.TaxRateService);
        }

        protected OrderService OrderService { get; set; }

        protected IOrderFulfillmentService OrderFulfillmentService { get; set; }

        protected IProductRepository ProductRepository { get; set; }

        protected ITaxRateService TaxRateService { get; set; }

        [Test]
        public void PlaceOrder_WhenItemsAreNotUnique()
        {
            // Arrange
            var order = MockRepository.GenerateStub<Order>();
            order.Stub(o => o.OrderItemsAreUniqueByProduct()).Return(false);

            // Act
            var exception = Assert.Throws<ValidationFailedException>(() => this.OrderService.PlaceOrder(order));
        
            // Assert
            Assert.That(exception.Reasons.Contains("Order Items are not unique by product."));
        }

        [Test]
        public void PlaceOrder_WhenItemsAreUnique()
        {
            // Arrange
            var orderService = new OrderService(null, OrderFulfillmentService, TaxRateService);

            var order = MockRepository.GenerateStub<Order>();
            order.Stub(o => o.OrderItemsAreUniqueByProduct()).Return(true);

            // Act
            OrderFulfillmentService.Expect(ofs => ofs.Fulfill(order)).Return(new OrderConfirmation());

            // Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order)) ;
        }

        [Test]
        public void PlaceOrder_SomeItemsAreNotInStock()
        {
            // Arrange
            var products = Builder<Product>
                .CreateListOfSize(3)
                .Build()
                ;

            var orderItems = Builder<OrderItem>
                .CreateListOfSize(3)
                .Build()
                .ToList()
                ;
            for (var i = 0; i < orderItems.Count; i++)
            {
                var product = products[i];
                orderItems[i].Product = product;
                ProductRepository.Stub(pr => pr.IsInStock(product.Sku)).Return(false);
            }

            var order = new Order()
            {
                OrderItems = orderItems,
            };

            // Act
           var exception = Assert.Throws<ValidationFailedException>(() => OrderService.PlaceOrder(order));

            // Assert
            Assert.That(exception.Reasons.Contains("Some products are out of stock."));
        }

        [Test]
        public void PlaceOrder_AllItemsAreInStock()
        {
            // Arrange
            var order = CreateAValidOrder(ProductRepository);
            
            // Act
            OrderFulfillmentService.Expect(ofs => ofs.Fulfill(order)).Return(new OrderConfirmation());

            // Assert
            Assert.DoesNotThrow(() => OrderService.PlaceOrder(order));
        }

        private static Order CreateAValidOrder(IProductRepository productRepository)
        {
            
            var products = Builder<Product>
                .CreateListOfSize(3)
                .Build()
                ;

            var orderItems = Builder<OrderItem>
                .CreateListOfSize(3)
                .Build()
                .ToList()
                ;

            for (var i = 0; i < orderItems.Count; i++)
            {
                var product = products[i];
                orderItems[i].Product = product;
                productRepository.Stub(pr => pr.IsInStock(product.Sku)).Return(true);
            }

            var order = new Order()
                {
                    OrderItems = orderItems,
                };
            return order;
        }

        [Test]
        public void PlaceOrder_WhenOrderIsValid()
        {
            // Arrange
            var order = CreateAValidOrder(ProductRepository);
            OrderFulfillmentService.Expect(ofs => ofs.Fulfill(order)).Return(new OrderConfirmation());

            // Act
            var summary = OrderService.PlaceOrder(order);

            // Assert
            Assert.That((object) summary, Is.Not.Null);
            OrderFulfillmentService.VerifyAllExpectations();
        }

        [Test]
        public void PlaceOrder_ContainingOrderFulfillmentConfirmationNumber()
        {
            // Arrange
            var order = CreateAValidOrder(ProductRepository);
            var orderConfirmation = new OrderConfirmation()
                {
                    OrderNumber = "LAQ4567"
                };
            OrderFulfillmentService.Expect(ofs => ofs.Fulfill(order)).Return(orderConfirmation);

            // Act
            var summary = OrderService.PlaceOrder(order);

            // Assert
            Assert.That(summary.OrderNumber, Is.EqualTo(orderConfirmation.OrderNumber));
        }

        [Test]
        public void PlaceOrder_ContainingOrderFulfillmentOrderID()
        {
            // Arrange
            var order = CreateAValidOrder(ProductRepository);
            var orderConfirmation = new OrderConfirmation()
            {
                OrderId = 5433
            };
            OrderFulfillmentService.Expect(ofs => ofs.Fulfill(order)).Return(orderConfirmation);

            // Act
            var summary = OrderService.PlaceOrder(order);
            
            // Assert
            Assert.That(summary.OrderId, Is.EqualTo(orderConfirmation.OrderId));
        }

        [Test]
        public void PlaceOrder_ContainsApplicableTaxesForCustomers()
        {
            // Arrange
            var order = CreateAValidOrder(ProductRepository);
            OrderFulfillmentService.Expect(ofs => ofs.Fulfill(order)).Return(new OrderConfirmation());

            var taxEntry = Builder<TaxEntry>
                .CreateListOfSize(3)
                .Build()
                ;

            var customer = new Customer()
                {
                    PostalCode = "98168",
                    Country = "USA"
                    
                };

            TaxRateService.Stub(o => o.GetTaxEntries(customer.PostalCode, customer.Country).Return(taxEntry));

            // Act
            var summary = OrderService.PlaceOrder(order);

            // Assert
            Assert.That(summary.Taxes, Is.EqualTo(taxEntry));
        }

    }
}
