using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using NUnit.Framework;
using Rhino.Mocks;
using Rhino;
using OrderEntryMockingPractice.Services;
using OrderEntryMockingPractice.Models;

namespace OrderEntryMockingPracticeTests
{
    [TestFixture]
    class OrderServiceTests
    {
        [SetUp]
        public void SetUp(){
            this.MockProductRepository = MockRepository.GenerateMock<IProductRepository>();
            this.MockOrderFulfillmentService = MockRepository.GenerateMock<IOrderFulfillmentService>();
            this.MockCustomerRepository = MockRepository.GenerateMock<ICustomerRepository>();
            this.MockTaxRateService = MockRepository.GenerateMock<ITaxRateService>();
            this.MockEmailService = MockRepository.GenerateMock<IEmailService>();
            this.orderService = new OrderService(MockProductRepository, MockOrderFulfillmentService, MockCustomerRepository, MockTaxRateService, MockEmailService);
        }

        protected IProductRepository MockProductRepository { get; set; }
        protected IOrderFulfillmentService MockOrderFulfillmentService { get; set; }
        protected ICustomerRepository MockCustomerRepository { get; set; }
        protected ITaxRateService MockTaxRateService { get; set; }
        protected IEmailService MockEmailService { get; set; }
        protected OrderService orderService { get; set; }

        [Test]
        public void PlaceOrder_WhenOrderItemsAreNotUnique_ThrowsValidationFailedException()
        {
            // Arrange
            var order = MockRepository.GenerateStub<Order>();
            order
                .Stub(o => o.OrderItemsAreUniqueByProduct())
                .Return(false);
            
            // Act
            var exception = Assert.Throws<ValidationFailedException>(() => orderService.PlaceOrder(order));

            // Assert
            Assert.That(exception.Reasons.Contains("Order Items are not unique by product."));
        }

        [Test]
        public void PlaceOrder_WhenOrderItemsAreUnique_DoesNotThrowValidationFailedException()
        {
            // Arrange
            var order = MockRepository.GenerateStub<Order>();
            order
                .Stub(o => o.OrderItemsAreUniqueByProduct())
                .Return(true);

            StubCustomer(order);
            StubTaxEntries();
            StubOrderConfirmation(order);

            // Act / Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order));
        }

        [Test]
        public void PlaceOrder_SomeProductsAreNotInStock_ThrowsValidationFailedException()
        {
            // Arrange
           var order = CreateInvalidOrderContainingProductsOutOfStock(MockProductRepository);

            // Act
            var exception = Assert.Throws<ValidationFailedException>(() => orderService.PlaceOrder(order));

            // Assert
            Assert.That(exception.Reasons.Contains("Some products are out of stock."));
        }

        private static Order CreateInvalidOrderContainingProductsOutOfStock(IProductRepository mockProductRepository)
        {
            var products = Builder<Product>
                .CreateListOfSize(3)
                .Build();

            var orderItems = Builder<OrderItem>
                .CreateListOfSize(3)
                .Build()
                .ToList();

            for (var i = 0; i < orderItems.Count; i++)
            {
                var product = products[i];
                orderItems[i].Product = product;
                mockProductRepository
                    .Stub(pr => pr.IsInStock(product.Sku))
                    .Return(false);
            }

            var order = new Order()
                        {
                            OrderItems = orderItems
                        };

            return order;
        }

        [Test]
        public void PlaceOrder_AllOrderItemsInStock_DoesNotThrowValidationFailedException()
        {
            // Arrange
           var order = CreateAValidOrder(MockProductRepository);

            StubCustomer(order);
            StubTaxEntries();
            StubOrderConfirmation(order);

            // Act / Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order));
        }

        private static Order CreateAValidOrder(IProductRepository mockProductRepository)
        {
            var products = Builder<Product>
                .CreateListOfSize(3)
                .Build();

            var orderItems = Builder<OrderItem>
                .CreateListOfSize(3)
                .Build()
                .ToList();

            for (var i = 0; i < orderItems.Count; i++)
            {
                var product = products[i];
                orderItems[i].Product = product;
                mockProductRepository
                    .Stub(pr => pr.IsInStock(product.Sku))
                    .Return(true);
            }

            var order = new Order()
                        {
                            OrderItems = orderItems
                        };

            return order;
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummary()
        {
            // Arrange
            var order = CreateAValidOrder(MockProductRepository);

            StubCustomer(order);
            StubTaxEntries();
            StubOrderConfirmation(order);

            // Act
            var result = orderService.PlaceOrder(order);

            // Assert
            Assert.That(result,Is.Not.Null);
            Assert.That(result.CustomerId, Is.EqualTo(order.CustomerId));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_OrderIsSubmittedToOrderFulfillmentService()
        {
            // Arrange
           var order = CreateAValidOrder(MockProductRepository);

            StubCustomer(order);
            StubTaxEntries();

            MockOrderFulfillmentService
                .Expect(ofs => ofs.Fulfill(order))
                .Return(new OrderConfirmation());

            // Act / Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order));
            MockOrderFulfillmentService.VerifyAllExpectations();
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryContainingOrderNumberGeneratedByOrderFulfillmentService()
        {
            // Arrange
            var order = CreateAValidOrder(MockProductRepository);

            StubCustomer(order);
            StubTaxEntries();
            
            var orderConfirmation = CreateValidOrderConfirmation();
            MockOrderFulfillmentService
                .Stub(ofs => ofs.Fulfill(order))
                .Return(orderConfirmation);
            
            // Act
            var orderSummary = orderService.PlaceOrder(order);
            var result = orderSummary.OrderNumber;
            var expected = orderConfirmation.OrderNumber;

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        private static OrderConfirmation CreateValidOrderConfirmation()
        {
            return new OrderConfirmation()
                {
                    OrderNumber = "AL435DSD",
                    OrderId = 3242
                };
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryContainingOrderIDGeneratedByOrderFulfillmentService()
        {
            // Arrange
            var order = CreateAValidOrder(MockProductRepository);

            StubCustomer(order);
            StubTaxEntries();

            var orderConfirmation = CreateValidOrderConfirmation();
            MockOrderFulfillmentService
                .Stub(ofs => ofs.Fulfill(order))
                .Return(orderConfirmation);
            
            // Act
            var orderSummary = orderService.PlaceOrder(order);
            var result = orderSummary.OrderId;
            var expected = orderConfirmation.OrderId;

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryWithTaxEntries()
        {
            // Arrange
            var customer = CreateValidCustomer();
            var taxEntriesList = CreateValidTaxEntriesList();

            var order = CreateAValidOrder(MockProductRepository);
            order.CustomerId = customer.CustomerId;

            StubOrderConfirmation(order);

            MockCustomerRepository
              .Stub(cr => cr.Get(order.CustomerId))
              .Return(customer);
            
            MockTaxRateService
                .Stub(trs => trs.GetTaxEntries(customer.PostalCode, customer.Country))
                .Return(taxEntriesList);

            // Act
            var orderSummary = orderService.PlaceOrder(order);
            var orderSummaryTaxEntries = orderSummary.Taxes;

            // Assert
            Assert.That(orderSummaryTaxEntries, Is.EqualTo(taxEntriesList));
        }

        private static IEnumerable<TaxEntry> CreateValidTaxEntriesList()
        {
            var taxEntry = CreateValidTaxEntry();

            var taxEntriesList = new List<TaxEntry>();
            taxEntriesList.Add(taxEntry);
            return taxEntriesList;
        }

        private static TaxEntry CreateValidTaxEntry()
        {
            return new TaxEntry()
                {
                    Description = "State Sales tax",
                    Rate = 9.0
                };
        }

        private static Customer CreateValidCustomer()
        {
            var customer = new Customer()
                {
                    CustomerId = 123,
                    AddressLine1 = "5th ave s",
                    City = "Seattle",
                    Country = "USA",
                    CustomerName = "Bob",
                    EmailAddress = "Bobby7@gmail.com",
                    PostalCode = "98168",
                    StateOrProvince = "WA"
                };
            return customer;
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryWithNetTotal()
        {
            // Arrange
            var orderItems = CreateListOfOrderItems();
            var order = new Order()
                        {
                            OrderItems = orderItems
                        };

            StubCustomer(order);
            StubTaxEntries();
            StubOrderConfirmation(order);

            // Act
            double expectedResult = 0;
            foreach(var orderItem in orderItems)
            {
                expectedResult += (((double)orderItem.Product.Price) * orderItem.Quantity);
            }

            var orderSummary = orderService.PlaceOrder(order);
            var result = orderSummary.NetTotal;

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryContainingOrderTotal()
        {
            // Arrange
            var orderItems = CreateListOfOrderItems();
            var order = new Order()
                        {
                            CustomerId = 123,
                            OrderItems = orderItems
                        };

            var customer = CreateValidCustomer();
            var taxEntry = CreateValidTaxEntry();
            var taxEntriesList = new List<TaxEntry> {taxEntry};

            StubOrderConfirmation(order);

            MockCustomerRepository
                .Stub(cr => cr.Get(order.CustomerId))
                .Return(customer);

            MockTaxRateService
                .Stub(trs => trs.GetTaxEntries(customer.PostalCode, customer.Country))
                .Return(taxEntriesList);

            // Act
            var orderSummary = orderService.PlaceOrder(order);
            var result = orderSummary.Total;

            double tax = 0;
            foreach(var entry in orderSummary.Taxes)
            {
                tax += orderSummary.NetTotal * entry.Rate;
            }

            var expectedResult = tax + orderSummary.NetTotal;
         
            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ConfirmationEmailIsSentToCustomer()
        {
            // Arrange
            var order = CreateAValidOrder(MockProductRepository);

            StubCustomer(order);
            StubTaxEntries();
            StubOrderConfirmation(order);

            var customerId = order.CustomerId;
            var orderId = MockOrderFulfillmentService.Fulfill(order).OrderId;

            MockEmailService
                .Expect(es => es.SendOrderConfirmationEmail(customerId,orderId));

            // Act / Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order));
            MockEmailService.VerifyAllExpectations();
        }

        [Test] 
        public void GetCustomerData__CustomerIdIsValid_ReturnCustomerContainingData()
        {
            // Arrange
            var expectedCustomer = CreateValidCustomer();
            var order = new Order()
                        {
                            CustomerId = expectedCustomer.CustomerId
                        };

            StubTaxEntries();
            StubOrderConfirmation(order);

            MockCustomerRepository
                .Stub(cr => cr.Get(order.CustomerId))
                .Return(expectedCustomer);

            // Act
            var result = orderService.GetCustomer(order.CustomerId);

            // Assert 
            Assert.That(result, Is.EqualTo(expectedCustomer));
        }

        [Test]
        public void GetTaxRates__CustomerIdIsValid_ReturnListOfTaxRates()
        {
            // Arrange
            var customer = CreateValidCustomer();
            var order = new Order()
                        {
                            CustomerId = customer.CustomerId
                        };

            var taxEntry = CreateValidTaxEntry();
            var taxEntries = new List<TaxEntry> {taxEntry};

            StubOrderConfirmation(order);
            StubCustomer(order);

            MockTaxRateService
                .Stub(trs => trs.GetTaxEntries(customer.PostalCode, customer.Country))
                .Return(taxEntries);

            // Act
            var result = orderService.GetTaxRates(customer.PostalCode, customer.Country);

            // Assert 
            Assert.That(result, Is.EqualTo(taxEntries));
        }

        private void StubOrderConfirmation(Order order)
        {
            MockOrderFulfillmentService
                .Stub(ofs => ofs.Fulfill(order))
                .Return(new OrderConfirmation());
        }

        private void StubTaxEntries()
        {
            MockTaxRateService
                .Stub(trs => trs.GetTaxEntries("", ""))
                .Return(new List<TaxEntry>());
        }

        private void StubCustomer(Order order)
        {
            MockCustomerRepository
                .Stub(cr => cr.Get(order.CustomerId))
                .Return(new Customer());
        }

        private List<OrderItem> CreateListOfOrderItems()
        {
            var orderItems = new List<OrderItem>();

            for (int i = 0; i < 3; i++)
            {
                orderItems.Add(new OrderItem());
            }

            foreach (var orderItem in orderItems)
            {
                orderItem.Product = new Product()
                                    {
                                        Price = 5
                                    };
                orderItem.Quantity = 2;
                OrderItem item = orderItem;
                MockProductRepository
                    .Stub(pr => pr.IsInStock(item.Product.Sku))
                    .Return(true);
            }
            return orderItems;
        }
    }
}
