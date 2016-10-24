using System.Collections.Generic;
using System.Xml.Serialization;

namespace SmvLibrary
{
    [XmlRootAttribute(Namespace = "")]
    public class SMVCloudConfig
    {
        [XmlElementAttribute("StorageConnectionString")]
        public StorageConnectionStringElement StorageConnectionString;

        [XmlElementAttribute("ServiceBusConnectionString")]
        public ServiceBusConnectionStringElement ServiceBusConnectionString;
    }

    [XmlRootAttribute("StorageConnectionString", Namespace = "")]
    public class StorageConnectionStringElement
    {
        [XmlAttributeAttribute()]
        public string value { get; set; }
    }

    [XmlRootAttribute("ServiceBusConnectionString", Namespace = "")]
    public class ServiceBusConnectionStringElement
    {
        [XmlAttributeAttribute()]
        public string value { get; set; }
    }
}