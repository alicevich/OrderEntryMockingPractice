using System;
using System.Collections.Generic;

namespace OrderEntryMockingPractice.Models
{
    public class OrderSummary
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public int CustomerId { get; set; }

        public List<OrderItem> OrderItems { get; set; }
        public double NetTotal { get; set; }
        public IEnumerable<TaxEntry> Taxes { get; set; }
        public double Total { get; set; }
        public DateTime EstimatedDeliveryDate { get; set; }
    }

    
}