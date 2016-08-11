﻿using System.Collections.Generic;
using System.Linq;
using AgGateway.ADAPT.ApplicationDataModel.ADM;
using AgGateway.ADAPT.ApplicationDataModel.Common;
using AgGateway.ADAPT.ApplicationDataModel.LoggedData;
using AgGateway.ADAPT.ISOv4Plugin.Extensions;
using AgGateway.ADAPT.ISOv4Plugin.Models;
using AgGateway.ADAPT.ISOv4Plugin.Writers;

namespace AgGateway.ADAPT.ISOv4Plugin.ExportMappers
{
    public interface ITaskMapper
    {
        IEnumerable<TSK> Map(IEnumerable<LoggedData> loggedData, Catalog catalog, string taskDataPath, int numberOfExistingTasks, TaskDocumentWriter writer, bool includeIfPrescription = true);
    }

    public class TaskMapper : ITaskMapper
    {
        private readonly ITimeMapper _timeMapper;
        private readonly ITlgMapper _tlgMapper;

        public TaskMapper() : this(new TimeMapper(), new TlgMapper())
        {
            
        }

        public TaskMapper(ITimeMapper timeMapper, ITlgMapper tlgMapper)
        {
            _timeMapper = timeMapper;
            _tlgMapper = tlgMapper;
        }

        public IEnumerable<TSK> Map(IEnumerable<LoggedData> loggedData, Catalog catalog, string taskDataPath, int numberOfExistingTasks, TaskDocumentWriter writer, bool includeIfPrescription = true)
        {
            if (loggedData == null)
                yield break;

            var loggedDataList = null as List<LoggedData>; 
            if(includeIfPrescription)
                loggedDataList = loggedData.ToList();
            else
                loggedDataList = loggedData.Where(x => x.OperationData != null && x.OperationData.All(y => y.PrescriptionId == null)).ToList();

            for (int i = 0; i < loggedDataList.Count(); ++i)
            {
                yield return Map(loggedDataList[i], catalog, taskDataPath, numberOfExistingTasks + (i+1), writer);
            }
        }

        private TSK Map(LoggedData loggedData, Catalog catalog, string taskDataPath, int taskNumber, TaskDocumentWriter taskDocumentWriter)
        {
            var taskId = "TSK" + taskNumber;
            taskDocumentWriter.Ids.Add(taskId, loggedData.Id);

            var tsk = new TSK
            {
                A = taskId,
                B = loggedData.Description,
                C = FindGrowerId(loggedData.GrowerId, catalog),
                D = FindFarmId(loggedData.FarmId, catalog),
                E = FindFieldId(loggedData.FieldId, catalog),
                G = TSKG.Item4,
                Items = MapItems(loggedData, catalog, taskDataPath, taskDocumentWriter)
            };

            return tsk;
        }

        private IWriter[] MapItems(LoggedData loggedData, Catalog catalog, string datacardPath, TaskDocumentWriter taskDocumentWriter)
        {
            var times = FindAndMapTimes(loggedData.TimeScopes);
            var tlgs = _tlgMapper.Map(loggedData.OperationData, datacardPath, taskDocumentWriter);

            var items = new List<IWriter>();
            
            if(times != null)
                items.AddRange(times);

            if(tlgs != null)
                items.AddRange(tlgs);

            return items.ToArray();
        }

        private IEnumerable<TIM> FindAndMapTimes(IEnumerable<TimeScope> timeScopes)
        {
            if (timeScopes == null)
                return null;

            return _timeMapper.Map(timeScopes);
        }

        private string FindFieldId(int? fieldId, Catalog catalog)
        {
            if (catalog.Fields == null)
                return null;

            var field = catalog.Fields.SingleOrDefault(x => x.Id.ReferenceId == fieldId);
            return field == null ? null : field.Id.FindIsoId();  
        }

        private string FindFarmId(int? farmId, Catalog catalog)
        {
            if(catalog.Farms == null)
                return null;

            var farm = catalog.Farms.SingleOrDefault(x => x.Id.ReferenceId == farmId);
            return farm == null ? null : farm.Id.FindIsoId();
        }

        private string FindGrowerId(int? growerId, Catalog catalog)
        {
            if (catalog.Growers == null)
                return null;

            var grower = catalog.Growers.SingleOrDefault(x => x.Id.ReferenceId == growerId);
            return grower == null ? null : grower.Id.FindIsoId();
        }
    }
}
