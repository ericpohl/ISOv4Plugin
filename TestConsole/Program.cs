using AgGateway.ADAPT.ApplicationDataModel.Common;
using AgGateway.ADAPT.ApplicationDataModel.Equipment;
using AgGateway.ADAPT.ApplicationDataModel.LoggedData;
using AgGateway.ADAPT.ApplicationDataModel.Representations;
using AgGateway.ADAPT.ISOv4Plugin;

namespace TestConsole;

class Program
{
    static void Main(string[] args)
    {
        var plugin = new Plugin();
        var models = plugin.Import(args[0]);
        foreach (var model in models)
        {
            var catalog = model.Catalog;
            Console.WriteLine("Catalog Products:");
            foreach (var catalogProduct in catalog.Products)
            {
                Console.WriteLine($" - {catalogProduct.Description}, Id = {catalogProduct.Id.ReferenceId}");
            }

            foreach (var loggedData in model.Documents.LoggedData)
            {
                foreach (var operation in loggedData.OperationData)
                {
                    Console.WriteLine($"Operation type: {operation.OperationType}");
                    if (operation.OperationType == OperationTypeEnum.Unknown || operation.OperationType == OperationTypeEnum.DataCollection)
                    {
                        Console.WriteLine($" - {operation.OperationType} operation type, skipping");
                        continue;
                    }

                    // Find device element uses for sections
                    int depth = 2;
                    var workingDatas = new List<(string, WorkingData)>();
                    foreach (DeviceElementUse deviceElementUse in operation.GetDeviceElementUses(depth).OrderBy(s => s.Order))
                    {
                        var deviceElementConfiguration = catalog.DeviceElementConfiguration(deviceElementUse.DeviceConfigurationId);
                        foreach (var workingData in deviceElementUse.GetWorkingDatas())
                        {
                            if (workingData.Representation.Code == "vrProductIndex")
                            {
                                workingDatas.Add((deviceElementConfiguration?.Description ?? "Unknown Device", workingData));
                            }
                        }
                    }

                    // Count products used in spatial records
                    var productCounts = new Dictionary<int, int>();
                    foreach (var record in operation.GetSpatialRecords())
                    {
                        //if (record.Timestamp is {Hour: 7, Minute: 23, Second: 51} && workingDatas.Any())
                        {
                            //Console.WriteLine($"record.Timestamp = {record.Timestamp}");
                            foreach (var (deviceName, workingData) in workingDatas)
                            {
                                int? productId = (int?)((NumericRepresentationValue)record.GetMeterValue(workingData))?.Value?.Value;
                                //Console.WriteLine($"{deviceName}: productId = {productId}");
                                if (productId.HasValue)
                                {
                                    if (productCounts.ContainsKey(productId.Value))
                                    {
                                        productCounts[productId.Value]++;
                                    }
                                    else
                                    {
                                        productCounts[productId.Value] = 1;
                                    }
                                }
                            }
                        }
                    }

                    Console.WriteLine($"Products used in operation:");
                    foreach ((int productId, int count) in productCounts)
                    {
                        var product = catalog.Products.FirstOrDefault(p => p.Id.ReferenceId == productId);
                        Console.WriteLine($" - {product?.Description ?? "Unknown Product"}, Id = {productId}, count = {count}");
                    }
                }
            }        
        }
    }
}
