using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITBoom_MSS.DataAccess.DbContextExtensions
{
    public static class DbContextExtensions 
    {
        public static DbCommand CreateCommand(this DbContext context, string storedProcedureName)
        {
            var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = storedProcedureName;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            return command;
        }

        public static DbCommand AddParams(this DbCommand command, IEnumerable<(string, object)> parameters)
        {
            foreach (var item in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = item.Item1;
                parameter.Value = item.Item2 ?? DBNull.Value;

                command.Parameters.Add(parameter);
            }
            return command;
        }

        public static async Task<IList<T>> ExecProcedureAsync<T>(this DbCommand command) where T : class
        {
            using (command)
            {
                if (command.Connection.State == System.Data.ConnectionState.Closed)
                    await command.Connection.OpenAsync();

                try
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        return reader.MapToList<T>();
                    }
                }
                finally
                {
                    command.Connection.Close();
                }
            }
        }

        public static IEnumerable<IDictionary<string, object>> ExecProcedureWithDynamicResult(this DbCommand command)
        {
            using (command)
            {
                if (command.Connection.State == System.Data.ConnectionState.Closed)
                    command.Connection.Open();

                try
                {
                    using (var reader = command.ExecuteReader())
                    {
                        var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                        foreach (IDataRecord record in reader as IEnumerable)
                        {
                            var resultDictionary = new Dictionary<string, object>() as IDictionary<string, object>;
                            foreach (var name in names)
                                resultDictionary[name] = record[name];

                            yield return resultDictionary;
                        }
                    }
                }
                finally
                {
                    command.Connection.Close();
                }
            }
        }

        private static IList<T> MapToList<T>(this DbDataReader dr)
        {
            var objList = new List<T>();
            var props = typeof(T).GetRuntimeProperties();

            var colMapping = dr.GetColumnSchema()
                .Where(x => props.Any(y => y.Name.ToLower() == x.ColumnName.ToLower()))
                .ToDictionary(key => key.ColumnName.ToLower());

            if (dr.HasRows)
            {
                while (dr.Read())
                {
                    T obj = Activator.CreateInstance<T>();
                    foreach (var prop in props)
                    {
                        var val = dr.GetValue(colMapping[prop.Name.ToLower()].ColumnOrdinal.Value);
                        prop.SetValue(obj, val == DBNull.Value ? null : val);
                    }
                    objList.Add(obj);
                }
            }
            return objList;
        }

    }
}
