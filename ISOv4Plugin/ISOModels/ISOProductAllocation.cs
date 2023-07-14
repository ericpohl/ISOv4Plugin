/*
 * ISO standards can be purchased through the ANSI webstore at https://webstore.ansi.org
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AgGateway.ADAPT.ApplicationDataModel.ADM;
using AgGateway.ADAPT.ApplicationDataModel.Products;
using AgGateway.ADAPT.ISOv4Plugin.ExtensionMethods;
using AgGateway.ADAPT.ISOv4Plugin.ISOEnumerations;
using AgGateway.ADAPT.ISOv4Plugin.ObjectModel;
using AgGateway.ADAPT.ISOv4Plugin.Mappers;

namespace AgGateway.ADAPT.ISOv4Plugin.ISOModels
{
    public class ISOProductAllocation : ISOElement
    {
        //Attributes
        public string ProductIdRef { get; set; }
        public string QuantityDDI { get; set; }
        public int? QuantityValue { get; set; }
        public ISOTransferMode? TransferMode { get { return (ISOTransferMode?)TransferModeInt; } set { TransferModeInt = (int?)value; } }
        private int? TransferModeInt { get; set; }
        public string DeviceElementIdRef { get; set; }
        public string ValuePresentationIdRef { get; set; }

        //Child Elements
        public ISOAllocationStamp AllocationStamp {get; set;}


        public override XmlWriter WriteXML(XmlWriter xmlBuilder)
        {
            xmlBuilder.WriteStartElement("PAN");
            xmlBuilder.WriteXmlAttribute("A", ProductIdRef);
            xmlBuilder.WriteXmlAttribute("B", QuantityDDI);
            xmlBuilder.WriteXmlAttribute("C", QuantityValue);
            xmlBuilder.WriteXmlAttribute<ISOTransferMode>("D", TransferMode);
            xmlBuilder.WriteXmlAttribute("E", DeviceElementIdRef);
            xmlBuilder.WriteXmlAttribute("F", ValuePresentationIdRef);
            if (AllocationStamp != null)
            {
                AllocationStamp.WriteXML(xmlBuilder);
            }
            xmlBuilder.WriteEndElement();
            return xmlBuilder;
        }

        public static ISOProductAllocation ReadXML(XmlNode node)
        {
            ISOProductAllocation item = new ISOProductAllocation();
            item.ProductIdRef = node.GetXmlNodeValue("@A");
            item.QuantityDDI = node.GetXmlNodeValue("@B");
            item.QuantityValue = node.GetXmlNodeValueAsNullableInt("@C");
            item.TransferModeInt = node.GetXmlNodeValueAsNullableInt("@D");
            item.DeviceElementIdRef = node.GetXmlNodeValue("@E");
            item.ValuePresentationIdRef = node.GetXmlNodeValue("@F");
            item.AllocationStamp = ISOAllocationStamp.ReadXML(node.SelectSingleNode("ASP"));
            return item;
        }

        public static IEnumerable<ISOProductAllocation> ReadXML(XmlNodeList nodes)
        {
            List<ISOProductAllocation> items = new List<ISOProductAllocation>();
            foreach (XmlNode node in nodes)
            {
                items.Add(ISOProductAllocation.ReadXML(node));
            }
            return items;
        }

        public override List<IError> Validate(List<IError> errors)
        {
            RequireString(this, x => x.ProductIdRef, 14, errors, "A");
            ValidateString(this, x => x.QuantityDDI, 4, errors, "B"); //DDI validation could be improved upon
            if (QuantityValue.HasValue) ValidateRange(this, x => x.QuantityValue.Value, 0, Int32.MaxValue - 1, errors, "C");
            if (TransferModeInt.HasValue) ValidateEnumerationValue(typeof(ISOTransferMode), TransferModeInt.Value, errors);
            ValidateString(this, x => x.DeviceElementIdRef, 14, errors, "E");
            ValidateString(this, x => x.ValuePresentationIdRef, 14, errors, "F");
            if (AllocationStamp != null) AllocationStamp.Validate(errors);
            return errors;
        }
    }

    public class ProductAllocationMap : IEnumerable<KeyValuePair<string, List<ISOProductAllocation>>>
    {
        private Dictionary<string, List<ISOProductAllocation>> _productAllocations;
        private TaskDataMapper TaskDataMapper { get; }

        public ProductAllocationMap(Dictionary<string, List<ISOProductAllocation>> dict, TaskDataMapper taskDataMapper)
        {
            _productAllocations = dict ?? throw new ArgumentNullException(nameof(dict));
            TaskDataMapper = taskDataMapper;
        }


        public IEnumerator<KeyValuePair<string, List<ISOProductAllocation>>> GetEnumerator()
        {
            return _productAllocations.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _productAllocations.GetEnumerator();
        }        

        public bool IsEmpty => !_productAllocations.Any();
        public KeyValuePair<string, List<ISOProductAllocation>> First() => _productAllocations.First();

        public List<ISOProductAllocation> this[string detID]
        {
            get
            {
                return _productAllocations[detID];
                // TODO: Finish this out.
                throw new NotImplementedException();
            }
        }
        
        internal List<int> GetDistinctProductIDs(TaskDataMapper taskDataMapper)
        {
            HashSet<int> productIDs = new HashSet<int>();
            foreach (string detID in _productAllocations.Keys)
            {
                foreach (ISOProductAllocation pan in _productAllocations[detID])
                {
                    int? id = taskDataMapper.InstanceIDMap.GetADAPTID(pan.ProductIdRef);
                    if (id.HasValue)
                    {
                        productIDs.Add(id.Value);
                    }
                }
            }
            return productIDs.ToList();
        }

        /// <summary>
        /// Determine if the dictionary of product allocations governs the device element or one of its ancestors in the device hierarchy
        /// </summary>
        public bool GovernDevice(ISODeviceElement deviceElement)
        {
            return deviceElement.Ancestors.Any(element => _productAllocations.ContainsKey(element.DeviceElementId));           
        }

        internal List<List<string>> SplitElementsByProductProperties(HashSet<string> loggedDeviceElementIds, ISODevice dvc)
        {
            //This function splits device elements logged by single TimeLog into groups based
            //on product form/type referenced by these elements. This is done using following logic:
            // - determine used products forms and list of device element ids for each form
            // - for each product form determine device elements from all other forms
            // - remove these device elements and their children from a copy of device hierarchy elements
            // - this gives a list of device elements to keep for a product form
            var deviceElementIdsByProductForm = _productAllocations
                .SelectMany(x => x.Value.Select(y => new { Product = GetProductByProductAllocation(y), Id = x.Key }))
                .Where(x => x.Product != null)
                .GroupBy(x => new { x.Product.Form, x.Product.ProductType }, x => x.Id)
                .Select(x => x.Distinct().ToList())
                .ToList();

            List<List<string>> deviceElementGroups = new List<List<string>>();
            if (deviceElementIdsByProductForm.Count > 1)
            {
                var deviceHierarchyElement = TaskDataMapper.DeviceElementHierarchies.Items[dvc.DeviceId];

                var idsWithProduct = deviceElementIdsByProductForm.SelectMany(x => x).ToList();
                foreach (var deviceElementIds in deviceElementIdsByProductForm)
                {
                    var idsToRemove = idsWithProduct.Except(deviceElementIds).ToList();
                    var idsToKeep = deviceHierarchyElement.FilterDeviceElementIds(idsToRemove);

                    deviceElementGroups.Add(loggedDeviceElementIds.Intersect(idsToKeep).ToList());
                }
            }
            else
            {
                deviceElementGroups.Add(loggedDeviceElementIds.ToList());
            }

            return deviceElementGroups;
        }

        private Product GetProductByProductAllocation(ISOProductAllocation pan)
        {
            var adaptProductId = TaskDataMapper.InstanceIDMap.GetADAPTID(pan.ProductIdRef);
            var adaptProduct = TaskDataMapper.AdaptDataModel.Catalog.Products.FirstOrDefault(x => x.Id.ReferenceId == adaptProductId);

            // Add an error if ProductAllocation is referencing non-existent product
            if (adaptProduct == null)
            {
                TaskDataMapper.AddError($"ProductAllocation referencing Product={pan.ProductIdRef} skipped since no matching product found");
            }
            return adaptProduct;
        }
    }
}
