using System;
using System.Collections.Generic;
using System.Linq;
using OrderEntryMockingPractice.Models;

namespace OrderEntryMockingPractice.Services
{
    public class OrderService
    {
        private IProductRepository productRepository;
        private IOrderFulfillmentService orderFulfillmentService;
        private ICustomerRepository customerRepository;
        private ITaxRateService taxRateService;
        private IEmailService emailService;

        public OrderService(IProductRepository productRepository, IOrderFulfillmentService orderFulfillmentService, ICustomerRepository customerRepository, ITaxRateService taxRateService, IEmailService emailService)
        {
            this.productRepository = productRepository;
            this.orderFulfillmentService = orderFulfillmentService;
            this.customerRepository = customerRepository;
            this.taxRateService = taxRateService;
            this.emailService = emailService;
        }

        public OrderSummary PlaceOrder(Order order)
        {
            AssertIsValidOrder(order);
            var orderConfirmation = orderFulfillmentService.Fulfill(order);
            var customer = customerRepository.Get(order.CustomerId);
            var taxEntries = taxRateService.GetTaxEntries(customer.PostalCode, customer.Country);
            var netTotal = GetNetTotal(order);
            var orderTotal = GetOrderTotal(order, taxEntries);
            emailService.SendOrderConfirmationEmail(customer.CustomerId, orderConfirmation.OrderId);

            return new OrderSummary()
            {
                OrderNumber = orderConfirmation.OrderNumber,
                OrderId = orderConfirmation.OrderId,
                Taxes = taxEntries,
                NetTotal = netTotal,
                Total = orderTotal
            };
        }

        private double GetOrderTotal(Order order, IEnumerable<TaxEntry> taxEntries)
        {
            double tax = 0;
            double netTotal = GetNetTotal(order);
            if (taxEntries != null)
            {
                foreach (var taxEntry in taxEntries)
                {
                    tax += (netTotal * (taxEntry.Rate/100));
                }
            } 
            return netTotal + tax;
        }

        private double GetNetTotal(Order order)
        {
            double netTotal = 0;
            foreach(var item in order.OrderItems)
            {
                netTotal += (((double) item.Product.Price) * item.Quantity);
            }
            return netTotal;
        }

        private void AssertIsValidOrder(Order order)
        {
            if (!order.OrderItemsAreUniqueByProduct())
            {
                throw new ValidationFailedException("Order Items are not unique by product.");
            }

            if (!AllProductsAreInStock(order))
            {
                throw new ValidationFailedException("Some products are out of stock.");
            }
        }

        private bool AllProductsAreInStock(Order order)
        {
            foreach (var item in order.OrderItems)
            {
                if (!productRepository.IsInStock(item.Product.Sku))
                {
                    return false;
                }
            }
            return true;
        }

        public Customer GetCustomer(int customerID)
        {
            return customerRepository.Get(customerID);
        }

        public IEnumerable<TaxEntry> GetTaxRates(string postalCode, string country)
        {
            return taxRateService.GetTaxEntries(postalCode, country);
        }
    }
}


