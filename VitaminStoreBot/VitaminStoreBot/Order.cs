using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VitaminStoreBot
{
    public class Order
    {
        public long ChatId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerSurname { get; set; }
        public string CustomerPatronymic { get; set; }
        public string CustomerPhone { get; set; }
        public string PaymentMethod { get; set; }
        public string DeliveryAddress { get; set; }
        public Dictionary<string, int> Cart { get; set; }
    }
}
