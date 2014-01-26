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
            this.mockProductRepository = MockRepository.GenerateMock<IProductRepository>();
            this.mockOrderFulfillmentService = MockRepository.GenerateMock<IOrderFulfillmentService>();
            this.mockCustomerRepository = MockRepository.GenerateMock<ICustomerRepository>();
            this.mockTaxRateService = MockRepository.GenerateMock<ITaxRateService>();
            this.mockEmailService = MockRepository.GenerateMock<IEmailService>();
        }

        protected IProductRepository mockProductRepository { get; set; }
        protected IOrderFulfillmentService mockOrderFulfillmentService { get; set; }
        protected ICustomerRepository mockCustomerRepository { get; set; }
        protected ITaxRateService mockTaxRateService { get; set; }
        protected IEmailService mockEmailService { get; set; }

        [Test]
        public void PlaceOrder_WhenOrderItemsAreNotUnique_ThrowsValidationFailedException()
        {
            // Arrange
            var orderService = new OrderService(null, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = MockRepository.GenerateStub<Order>();

            // Act
            order
                .Stub(o => o.OrderItemsAreUniqueByProduct())
                .Return(false);

            var exception = Assert.Throws<ValidationFailedException>(() => orderService.PlaceOrder(order));

            // Assert
            Assert.That(exception.Reasons.Contains("Order Items are not unique by product."));
        }

        [Test]
        public void PlaceOrder_WhenOrderItemsAreUnique_DoesNotThrowValidationFailedException()
        {
            // Arrange
            var orderService = new OrderService(null, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = MockRepository.GenerateStub<Order>();

            // Act
            order
                .Stub(o => o.OrderItemsAreUniqueByProduct())
                .Return(true);

            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();
            GenerateNewOrderConfirmation(order);

            // Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order));
        }

        [Test]
        public void PlaceOrder_SomeProductsAreNotInStock_ThrowsValidationFailedException()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateInvalidOrderContainingProductsOutOfStock(mockProductRepository);

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
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateAValidOrder(mockProductRepository);

            // Act
            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();
            GenerateNewOrderConfirmation(order);

            // Assert
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
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateAValidOrder(mockProductRepository);

            // Act
            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();
            GenerateNewOrderConfirmation(order);

            var result = orderService.PlaceOrder(order);

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void PlaceOrder_OrderIsValid_OrderIsSubmittedToOrderFulfillmentService()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateAValidOrder(mockProductRepository);

            // Act
            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();

            mockOrderFulfillmentService
                .Expect(ofs => ofs.Fulfill(order))
                .Return(new OrderConfirmation());

            // Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order));
            mockOrderFulfillmentService.VerifyAllExpectations();
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryContainingOrderNumberGeneratedByOrderFulfillmentService()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateAValidOrder(mockProductRepository);

            string expectedOrderNumber = "AL435DSD";
            var orderConfirmation = new OrderConfirmation()
                                    {
                                        OrderNumber = expectedOrderNumber
                                    };

            // Act
            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();

            mockOrderFulfillmentService
                .Stub(ofs => ofs.Fulfill(order))
                .Return(orderConfirmation);

            var orderSummary = orderService.PlaceOrder(order);
            var result = orderSummary.OrderNumber;

            // Assert
            Assert.That(result, Is.EqualTo(expectedOrderNumber));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryContainingOrderIDGeneratedByOrderFulfillmentService()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateAValidOrder(mockProductRepository);

            int expectedOrderID = 3242;
            var orderConfirmation = new OrderConfirmation()
                                    {
                                        OrderId = expectedOrderID
                                    };

            // Act
            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();

            mockOrderFulfillmentService
                .Stub(ofs => ofs.Fulfill(order))
                .Return(orderConfirmation);

            var orderSummary = orderService.PlaceOrder(order);
            var result = orderSummary.OrderId;

            // Assert
            Assert.That(result, Is.EqualTo(expectedOrderID));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryWithTaxEntries()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateAValidOrder(mockProductRepository);
            order.CustomerId = 123;

            var customer = new Customer()
                            {
                                CustomerId = 123,
                                PostalCode = "98168",
                                Country = "USA"
                            };

            var taxEntry = new TaxEntry()
                            {
                                Description = "State Sales tax",
                                Rate = 9.0
                            };

            var taxEntriesList = new List<TaxEntry>();
            taxEntriesList.Add(taxEntry);

            // Act
            GenerateNewOrderConfirmation(order);

            mockCustomerRepository
                .Stub(cr => cr.Get(order.CustomerId))
                .Return(customer);

            mockTaxRateService
                .Stub(trs => trs.GetTaxEntries(customer.PostalCode, customer.Country))
                .Return(taxEntriesList);

            var orderSummary = orderService.PlaceOrder(order);
            var orderSummaryTaxEntries = orderSummary.Taxes;

            // Assert
            Assert.That(orderSummaryTaxEntries, Is.EqualTo(taxEntriesList));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ReturnsOrderSummaryWithNetTotal()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var orderItems = CreateListOfOrderItems();
            var order = new Order()
                        {
                            OrderItems = orderItems
                        };

            // Act
            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();
            GenerateNewOrderConfirmation(order);

            double expectedResult = 0;
            foreach (var orderItem in orderItems)
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
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var orderItems = CreateListOfOrderItems();
            var order = new Order()
                        {
                            CustomerId = 123,
                            OrderItems = orderItems
                        };

            var customer = new Customer()
                            {
                                CustomerId = 123,
                                PostalCode = "98168",
                                Country = "USA"
                            };

            var taxEntry = new TaxEntry()
                            {
                                Description = "State Sales tax",
                                Rate = 9.0
                            };

            var taxEntriesList = new List<TaxEntry>();
            taxEntriesList.Add(taxEntry);

            // Act
            GenerateNewOrderConfirmation(order);

            mockCustomerRepository
                .Stub(cr => cr.Get(order.CustomerId))
                .Return(customer);

            mockTaxRateService
                .Stub(trs => trs.GetTaxEntries(customer.PostalCode, customer.Country))
                .Return(taxEntriesList);

            var orderSummary = orderService.PlaceOrder(order);
            var orderSummaryTaxEntries = orderSummary.Taxes;

            double tax = 0;
            foreach (var entry in orderSummary.Taxes)
            {
                tax += (orderSummary.NetTotal * (entry.Rate / 100));
            }

            var expectedResult = tax + orderSummary.NetTotal;
            var result = orderSummary.Total;

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void PlaceOrder_OrderIsValid_ConfirmationEmailIsSentToCustomer()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = CreateAValidOrder(mockProductRepository);

            // Act
            GenerateNewCustomer(order);
            GenerateNewListOfTaxEntries();
            GenerateNewOrderConfirmation(order);
            mockEmailService
                .Expect(es => es.SendOrderConfirmationEmail(order.CustomerId, mockOrderFulfillmentService.Fulfill(order).OrderId));
        
            // Assert
            Assert.DoesNotThrow(() => orderService.PlaceOrder(order));
            mockEmailService.VerifyAllExpectations();
        }

        [Test] 
        public void GetCustomerData__CustomerIdIsValid_ReturnCustomerContainingData()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = new Order()
                        {
                            CustomerId = 123
                        };

            var expectedCustomer = new Customer()
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

            // Act
            GenerateNewListOfTaxEntries();
            GenerateNewOrderConfirmation(order);

            mockCustomerRepository
                .Stub(cr => cr.Get(order.CustomerId))
                .Return(expectedCustomer);
           
            var result = orderService.GetCustomer(order.CustomerId);

            // Assert 
            Assert.That(result, Is.EqualTo(expectedCustomer));
        }

        [Test]
        public void GetTaxRates__CustomerIdIsValid_ReturnListOfTaxRates()
        {
            // Arrange
            var orderService = new OrderService(mockProductRepository, mockOrderFulfillmentService, mockCustomerRepository, mockTaxRateService, mockEmailService);
            var order = new Order()
            {
                CustomerId = 123
            };

            var customer = new Customer()
            {
                CustomerId = 123,
                Country = "USA",
                PostalCode = "98168"
            };

            var taxEntries = new List<TaxEntry>();
            var taxEntry = new TaxEntry()
                            {
                                Description = "State Sales Tax",
                                Rate = 9.5
                            };
            taxEntries.Add(taxEntry);

            // Act
            GenerateNewOrderConfirmation(order);
            GenerateNewCustomer(order);

            mockTaxRateService
                .Stub(trs => trs.GetTaxEntries(customer.PostalCode, customer.Country))
                .Return(taxEntries);

            var result = orderService.GetTaxRates(customer.PostalCode, customer.Country);

            // Assert 
            Assert.That(result, Is.EqualTo(taxEntries));
        }

        private void GenerateNewOrderConfirmation(Order order)
        {
            mockOrderFulfillmentService
                .Stub(ofs => ofs.Fulfill(order))
                .Return(new OrderConfirmation());
        }

        private void GenerateNewListOfTaxEntries()
        {
            mockTaxRateService
                .Stub(trs => trs.GetTaxEntries("", ""))
                .Return(new List<TaxEntry>());
        }

        private void GenerateNewCustomer(Order order)
        {
            mockCustomerRepository
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
                mockProductRepository
                    .Stub(pr => pr.IsInStock(orderItem.Product.Sku))
                    .Return(true);
            }
            return orderItems;
        }
    }
}
