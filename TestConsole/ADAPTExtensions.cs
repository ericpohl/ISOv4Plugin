using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgGateway.ADAPT.ApplicationDataModel.ADM;
using AgGateway.ADAPT.ApplicationDataModel.Documents;
using AgGateway.ADAPT.ApplicationDataModel.Equipment;
using AgGateway.ADAPT.ApplicationDataModel.LoggedData;
using AgGateway.ADAPT.ApplicationDataModel.Logistics;
using AgGateway.ADAPT.ApplicationDataModel.Representations;
using AgGateway.ADAPT.ApplicationDataModel.Shapes;

using ADAPTProduct = AgGateway.ADAPT.ApplicationDataModel.Products;

namespace TestConsole;

/// <summary>
/// Extension methods to make the syntax of navigating through the ADAPT model (especially the Catalog) easier
/// </summary>
public static class ADAPTExtensions
{
    // Extensions on Catalog

    public static string? Grower(this Catalog catalog, int? growerId) =>
        growerId.HasValue ? catalog.Growers.FirstOrDefault(g => g.Id.ReferenceId == growerId.Value)?.Name : null;
    public static string? Farm(this Catalog catalog, int? farmId) =>
        farmId.HasValue ? catalog.Farms.FirstOrDefault(f => f.Id.ReferenceId == farmId.Value)?.Description : null;
    public static string? Field(this Catalog catalog, int? fieldId) =>
        fieldId.HasValue ? catalog.Fields.FirstOrDefault(f => f.Id.ReferenceId == fieldId.Value)?.Description : null;

    public static string? Crop(this Catalog catalog, int? cropId) =>
        cropId.HasValue ? catalog.Crops.FirstOrDefault(c => c.Id.ReferenceId == cropId.Value)?.Name : null;

    public static CropZone? CropZone(this Catalog catalog, int cropZoneId) => catalog.CropZones.FirstOrDefault(c => c.Id.ReferenceId == cropZoneId);

    public static ADAPTProduct.Product? Product(this Catalog catalog, int productId) => catalog.Products.FirstOrDefault(p => p.Id.ReferenceId == productId);

    public static EquipmentConfiguration? EquipmentConfiguration(this Catalog catalog, int equipConfigId) =>
        catalog.EquipmentConfigurations.FirstOrDefault(ec => ec.Id.ReferenceId == equipConfigId);

    public static EquipmentConfiguration? EquipmentConfiguration(this OperationData operationData, Catalog catalog) =>
        catalog.EquipmentConfigurations.FirstOrDefault(ec => ec.Id.ReferenceId == operationData.EquipmentConfigurationIds.FirstOrDefault());

    public static HitchPoint? VehicleHitch(this OperationData operationData, Catalog catalog)
    {
        Connector? connector1 = operationData.EquipmentConfiguration(catalog)?.Connector1(catalog);
        if (connector1 is null) return null;

        return catalog.HitchPoint(connector1.HitchPointId);
    }

    public static MachineConfiguration? MachineConfiguration(this OperationData operationData, Catalog catalog, LoggedData loggedDatum)
    {
        MachineConfiguration? machineConfiguration = null;
        Connector? connector1 = operationData.EquipmentConfiguration(catalog)?.Connector1(catalog);
        if (connector1 is not null)
        {
            machineConfiguration = catalog.MachineConfiguration(connector1.DeviceElementConfigurationId);
        }

        if (machineConfiguration is null && operationData.EquipmentConfigurationIds.Any())
        {
            // Sometimes Deere does not have a MachineConfiguration in the catalog that matches the operation EquipmentConfiguration connector1.
            // Alternatively, look in EquipmentConfigurationGroup
            int implementConfigurationId = operationData.EquipmentConfigurationIds.First();
            EquipmentConfiguration implementConfiguration = catalog.EquipmentConfiguration(implementConfigurationId)!;
            EquipmentConfiguration? twoPartConfiguration = loggedDatum.EquipmentConfigurationGroup?.EquipmentConfigurations?.FirstOrDefault(ec => ec.Connector2Id == implementConfiguration.Connector1Id);
            if (twoPartConfiguration is not null)
            {
                Connector? connector = twoPartConfiguration.Connector1(catalog);
                if (connector is not null)
                {
                    machineConfiguration = catalog.MachineConfiguration(connector.DeviceElementConfigurationId);
                }
            }
        }

        return machineConfiguration;
    }

    public static HitchPoint? ImplementHitch(this OperationData operationData, Catalog catalog)
    {
        Connector? connector2 = operationData.EquipmentConfiguration(catalog)?.Connector2(catalog);
        if (connector2 is null) return null;

        return catalog.HitchPoint(connector2.HitchPointId);
    }

    public static ImplementConfiguration? ImplementConfiguration(this OperationData operationData, Catalog catalog)
    {
        ImplementConfiguration? implementConfiguration = null;
        Connector? connector2 = operationData.EquipmentConfiguration(catalog)?.Connector2(catalog);
        if (connector2 is not null)
        {
            implementConfiguration = catalog.ImplementConfiguration(connector2.DeviceElementConfigurationId);
        }

        if (implementConfiguration is null)
        {
            // sometimes with Deere the implement is attached to Connector 1
            Connector? connector1 = operationData.EquipmentConfiguration(catalog)?.Connector1(catalog);
            if (connector1 is not null)
            {
                implementConfiguration = catalog.ImplementConfiguration(connector1.DeviceElementConfigurationId);
            }
        }

        return implementConfiguration;
    }

    public static Connector? Connector(this Catalog catalog, int? connectorId) =>
        connectorId.HasValue ? catalog.Connectors.FirstOrDefault(c => c.Id.ReferenceId == connectorId.Value) : null;

    public static DeviceElementConfiguration? DeviceElementConfiguration(this Catalog catalog, int deviceElementConfigurationId) =>
        catalog.DeviceElementConfigurations.FirstOrDefault(d => d.Id.ReferenceId == deviceElementConfigurationId);

    public static MachineConfiguration? MachineConfiguration(this Catalog catalog, int deviceElementConfigurationId) =>
        catalog.DeviceElementConfigurations.OfType<MachineConfiguration>().FirstOrDefault(d => d.Id.ReferenceId == deviceElementConfigurationId);

    public static ImplementConfiguration? ImplementConfiguration(this Catalog catalog, int implementConfigurationId) =>
        catalog.DeviceElementConfigurations.OfType<ImplementConfiguration>().FirstOrDefault(d => d.Id.ReferenceId == implementConfigurationId);

    public static SectionConfiguration? SectionConfiguration(this Catalog catalog, int sectionConfigurationId) =>
        catalog.DeviceElementConfigurations.OfType<SectionConfiguration>().FirstOrDefault(d => d.Id.ReferenceId == sectionConfigurationId);

    public static DeviceElement? DeviceElement(this Catalog catalog, int deviceElementId) =>
        catalog.DeviceElements.FirstOrDefault(deviceElement => deviceElement.Id.ReferenceId == deviceElementId);

    public static DeviceModel? DeviceModel(this Catalog catalog, int deviceModelId) =>
        catalog.DeviceModels.FirstOrDefault(d => d.Id.ReferenceId == deviceModelId);

    public static HitchPoint? HitchPoint(this Catalog catalog, int hitchPointId) =>
        catalog.HitchPoints.FirstOrDefault(h => h.Id.ReferenceId == hitchPointId);

    // Extensions on other classes
    public static Connector? Connector1(this EquipmentConfiguration equipmentConfiguration, Catalog catalog) =>
        catalog.Connector(equipmentConfiguration.Connector1Id);

    public static Connector? Connector2(this EquipmentConfiguration equipmentConfiguration, Catalog catalog) =>
        catalog.Connector(equipmentConfiguration.Connector2Id);

    public static Summary? Summary(this Documents documents, int summaryId) => documents.Summaries.FirstOrDefault(s => s.Id.ReferenceId == summaryId);

    public static NumericRepresentationValue? ByCode(this IEnumerable<NumericRepresentationValue> numericRepresentationValue, string code) =>
        numericRepresentationValue.FirstOrDefault(o => o.Representation.Code == code);

    public static string GetUOMCode(this WorkingData workingData)
    {
        string uom = "";
        if (workingData is NumericWorkingData numericWorkingData)
        {
            // Once in a while we see a null numericWorkingData.UnitOfMeasure here
            uom = numericWorkingData.UnitOfMeasure?.Code ?? "";
        }
        return uom;
    }


    public static WorkingData? ByCode(this IEnumerable<WorkingData> workingDatas, string code) =>
        workingDatas.FirstOrDefault(w => w.Representation != null && w.Representation.Code == code);
    
    /// <summary>
    /// Routes any dat data with Gen2 version 16 to the Precision Planting plugin vs. 
    /// the Climate plugin since the former appears to do better there.
    /// The newer Gen2 (version 30) works ok in both so I left that in Climate for now.
    /// </summary>
    /// <param name="datFile"></param>
    /// <returns></returns>
    internal static bool ShouldProcessDatFileWithPrecisionPlugin(this string datFile)
    {
        foreach (var line in File.ReadLines(datFile).Take(5))
        {
            if (line.StartsWith("file_format="))
            {
                var split = line.Split('=');
                if (int.TryParse(split[1], out int version) &&
                    version <= 16)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
