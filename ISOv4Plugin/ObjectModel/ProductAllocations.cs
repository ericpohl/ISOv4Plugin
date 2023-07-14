/*
 * ISO standards can be purchased through the ANSI webstore at https://webstore.ansi.org
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgGateway.ADAPT.ApplicationDataModel.Products;
using AgGateway.ADAPT.ISOv4Plugin.Mappers;

namespace AgGateway.ADAPT.ISOv4Plugin.ISOModels
{
    /// <summary>
    /// A wrapper around a dictionary of ISOProductAllocation lists, keyed by device element id,
    /// which contains additional logic to ascend the device element hierarchy if necessary.
    /// </summary>
    public class ProductAllocations : IEnumerable<KeyValuePair<string, List<ISOProductAllocation>>>
    {
        private readonly Dictionary<string, List<ISOProductAllocation>> _productAllocations;
        private readonly TaskDataMapper _taskDataMapper;

        public ProductAllocations(Dictionary<string, List<ISOProductAllocation>> dict, TaskDataMapper taskDataMapper)
        {
            _productAllocations = dict ?? throw new ArgumentNullException(nameof(dict));
            _taskDataMapper = taskDataMapper ?? throw new ArgumentNullException(nameof(taskDataMapper));
        }

        public IEnumerator<KeyValuePair<string, List<ISOProductAllocation>>> GetEnumerator() => _productAllocations.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _productAllocations.GetEnumerator();

        public List<ISOProductAllocation> this[string detID]
        {
            get
            {
                // If the device elment ID is in the dictionary directly, return its allocation list straightaway
                if (_productAllocations.TryGetValue(detID, out List<ISOProductAllocation> list))
                {
                    return list;
                }

                // Otherwise, go up the device element hierarchy
                ISODeviceElement deviceElement = _taskDataMapper.DeviceElementHierarchies.GetISODeviceElementFromID(detID);
                foreach (ISODeviceElement element in deviceElement.Ancestors)
                {
                    if (_productAllocations.TryGetValue(element.DeviceElementId, out list))
                    {
                        return list;
                    }
                }

                throw new KeyNotFoundException();
            }
        }
        
        internal List<int> GetDistinctProductIDs()
        {
            HashSet<int> productIDs = new HashSet<int>();
            foreach (string detID in _productAllocations.Keys)
            {
                foreach (ISOProductAllocation pan in _productAllocations[detID])
                {
                    int? id = _taskDataMapper.InstanceIDMap.GetADAPTID(pan.ProductIdRef);
                    if (id.HasValue)
                    {
                        productIDs.Add(id.Value);
                    }
                }
            }
            return productIDs.ToList();
        }

        /// <summary>
        /// Determine if the product allocations govern the device element or one of its ancestors in the device hierarchy
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
                var deviceHierarchyElement = _taskDataMapper.DeviceElementHierarchies.Items[dvc.DeviceId];

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
            var adaptProductId = _taskDataMapper.InstanceIDMap.GetADAPTID(pan.ProductIdRef);
            var adaptProduct = _taskDataMapper.AdaptDataModel.Catalog.Products.FirstOrDefault(x => x.Id.ReferenceId == adaptProductId);

            // Add an error if ProductAllocation is referencing non-existent product
            if (adaptProduct == null)
            {
                _taskDataMapper.AddError($"ProductAllocation referencing Product={pan.ProductIdRef} skipped since no matching product found");
            }
            return adaptProduct;
        }

        internal ProductAllocations WithElementHierarchies(List<string> elementHierarchyIds)
        {
            var productAllocations = _productAllocations
                .Where(x => elementHierarchyIds.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value);

            return new ProductAllocations(productAllocations, _taskDataMapper);
        }
    }
}
