using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Dapper.Extensions
{
    public static partial class DapperExtensions
    {
        /// <summary>
        /// 类型所有属性缓存
        /// </summary>
        private static readonly IDictionary<Type, PropertyInfo[]> propertiesCaches = new Dictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// 根据传入的 <paramref name="obj"/> 转换为 DynamicParameters
        /// </summary>
        /// <param name="obj">需要转为 DynamicParameters 的实例</param>
        /// <param name="exclude">需要排除的属性字段</param>
        /// <returns>DynamicParameters</returns>
        private static DynamicParameters CreateDynamicParameters(object obj, string[] keyNames, string[] exclude)
        {
            var parameters = new DynamicParameters();
            // 遍历所有属性
            foreach (PropertyInfo property in GetProperties(obj.GetType()))
            {
                var value = property.GetValue(obj, null);
                // 属性是否是排除列表中
                // 字段是为主键并且值为 null
                if (exclude?.Contains(property.Name) == true || (keyNames?.Contains(property.Name) == true && value == null))
                {
                    continue;
                }
                // 处理日期类型在使用 Odbc 时会溢出，需要明确参数精度
                if ((property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?)) && value != null)
                {
                    parameters.Add(property.Name, value, size: 23, precision: 0, scale: 3);
                }
                else
                {
                    parameters.Add(property.Name, value);
                }
            }
            return parameters;
        }

        /// <summary>
        /// 保存
        /// 添加或更新数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">添加或更新的实例</param>
        /// <param name="keyNames">主键</param>
        /// <param name="tableName">添加或更新的表名</param>
        /// <param name="transaction">事务</param>
        /// <param name="exclude">要排除的 <paramref name="obj"/> 属性</param>
        public static int? Save(this IDbConnection connection, object obj, string[] keyNames, string tableName = null, IDbTransaction transaction = null, params string[] exclude)
        {
            DynamicParameters parameters = CreateDynamicParameters(obj, keyNames, exclude);
            // 创建参数
            if (CheckExists(connection, obj, keyNames, tableName ?? obj.GetType().Name, transaction))
            {
                // 更新数据
                return Update(connection, parameters, keyNames, tableName ?? obj.GetType().Name, transaction);
            }
            else
            {
                // 添加数据
                return Insert(connection, parameters, tableName ?? obj.GetType().Name, transaction);
            }
        }

        /// <summary>
        /// 保存
        /// 添加或更新数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">添加或更新的实例</param>
        /// <param name="keyNames">主键</param>
        /// <param name="tableName">添加或更新的表名</param>
        /// <param name="transaction">事务</param>
        /// <param name="exclude">要排除的 <paramref name="obj"/> 属性</param>
        public static async Task<int?> SaveAsync(this IDbConnection connection, object obj, string[] keyNames, string tableName = null, IDbTransaction transaction = null, params string[] exclude)
        {
            DynamicParameters parameters = CreateDynamicParameters(obj, keyNames, exclude);
            // 创建参数
            if (await CheckExistsAsync(connection, obj, keyNames, tableName ?? obj.GetType().Name, transaction))
            {
                // 更新数据
                return await UpdateAsync(connection, parameters, keyNames, tableName ?? obj.GetType().Name, transaction);
            }
            else
            {
                // 添加数据
                return await InsertAsync(connection, parameters, tableName ?? obj.GetType().Name, transaction);
            }
        }

        /// <summary>
        /// 检查记录是否已存在
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">添加或更新的实例</param>
        /// <param name="keyNames">主键</param>
        /// <param name="tableName">添加或更新的表名</param>
        /// <param name="transaction">事务</param>
        /// <returns>记录已存在返回 true，不存在返回 false</returns>
        public static bool CheckExists(this IDbConnection connection, object obj, string[] keyNames, string tableName = null, IDbTransaction transaction = null)
        {
            var template = CreateCheckExistsTemplate(connection, obj, keyNames, tableName);
            // 以主键查询是否存在记录以此判断是添加或更新数据
            int count = connection.ExecuteScalar<int>(template.RawSql, obj, transaction);
            // 大于 0 数据库中已存在此笔记录
            return count > 0;
        }

        /// <summary>
        /// 检查记录是否已存在
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">添加或更新的实例</param>
        /// <param name="keyNames">主键</param>
        /// <param name="tableName">添加或更新的表名</param>
        /// <param name="transaction">事务</param>
        /// <returns>记录已存在返回 true，不存在返回 false</returns>
        public static async Task<bool> CheckExistsAsync(this IDbConnection connection, object obj, string[] keyNames, string tableName = null, IDbTransaction transaction = null)
        {
            var template = CreateCheckExistsTemplate(connection, obj, keyNames, tableName);
            // 以主键查询是否存在记录以此判断是添加或更新数据
            int count = await connection.ExecuteScalarAsync<int>(template.RawSql, obj, transaction);
            // 大于 0 数据库中已存在此笔记录
            return count > 0;
        }

        private static SqlBuilder.Template CreateCheckExistsTemplate(IDbConnection connection, object obj, string[] keyNames, string tableName = null)
        {
            var sqlBuilder = new SqlBuilder();
            foreach (var keyName in keyNames)
            {
                // 添加主键参数
                sqlBuilder.Where($"{keyName}={connection.GetParameterName(keyName)}");
            }
            return sqlBuilder.AddTemplate($"select count(*) from {tableName ?? obj.GetType().Name} /**where**/");
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="parameters">添加参数</param>
        /// <param name="tableName">添加数据的表</param>
        /// <param name="transaction">事务</param>
        /// <returns>返回自增主键</returns>
        public static int? Insert(this IDbConnection connection, DynamicParameters parameters, string tableName, IDbTransaction transaction = null)
        {
            var template = CreateInsertTemplate(connection, parameters, tableName);
            return connection.ExecuteScalar<int?>(template.RawSql, parameters, transaction);
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="parameters">添加参数</param>
        /// <param name="tableName">添加数据的表</param>
        /// <param name="transaction">事务</param>
        /// <returns>返回自增主键</returns>
        public static Task<int?> InsertAsync(this IDbConnection connection, DynamicParameters parameters, string tableName, IDbTransaction transaction = null)
        {
            var template = CreateInsertTemplate(connection, parameters, tableName);
            return connection.ExecuteScalarAsync<int?>(template.RawSql, parameters, transaction);
        }

        /// <summary>
        /// 生成 insert 模版
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="parameters">添加参数</param>
        /// <param name="tableName">添加数据的表</param>
        /// <returns></returns>
        private static SqlBuilder.Template CreateInsertTemplate(IDbConnection connection, DynamicParameters parameters, string tableName)
        {
            var sqlBuilder = new SqlBuilder();
            var paramNames = string.Join(",", parameters.ParameterNames.Select(name =>
            {
                sqlBuilder.Select(name);
                return GetParameterName(connection, name);
            }));
            return sqlBuilder.AddTemplate($"set nocount on;insert into {tableName}(/**select**/) values ({paramNames});select SCOPE_IDENTITY() id");
        }

        /// <summary>
        /// 向数据库添加数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">需要添加的数据</param>
        /// <param name="tableName">添加数据的表</param>
        /// <param name="transaction">事务</param>
        /// <param name="exclude">要排除的属性</param>
        /// <returns>返回自增主键</returns>
        public static int? Insert(this IDbConnection connection, object obj, string tableName = null, IDbTransaction transaction = null, params string[] exclude)
        {
            return Insert(connection, CreateDynamicParameters(obj, null, exclude), tableName ?? obj.GetType().Name, transaction);
        }

        /// <summary>
        /// 向数据库添加数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">需要添加的数据</param>
        /// <param name="tableName">添加数据的表</param>
        /// <param name="transaction">事务</param>
        /// <param name="exclude">要排除的属性</param>
        /// <returns>返回自增主键</returns>
        public static Task<int?> InsertAsync(this IDbConnection connection, object obj, string tableName = null, IDbTransaction transaction = null, params string[] exclude)
        {
            return InsertAsync(connection, CreateDynamicParameters(obj, null, exclude), tableName ?? obj.GetType().Name, transaction);
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="parameters">更新参数</param>
        /// <param name="tableName">更新的表名</param>
        /// <param name="keyNames">更新的主键/条件参数</param>
        /// <param name="transaction">事务</param>
        /// <returns>更新受影响的行数</returns>
        public static int Update(this IDbConnection connection, DynamicParameters parameters, string[] keyNames, string tableName, IDbTransaction transaction = null)
        {
            var template = CreateUpdateTemplate(connection, parameters, keyNames, tableName);
            return connection.Execute(template.RawSql, parameters, transaction);
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="parameters">更新参数</param>
        /// <param name="tableName">更新的表名</param>
        /// <param name="keyNames">更新的主键/条件参数</param>
        /// <param name="transaction">事务</param>
        /// <returns>更新受影响的行数</returns>
        public static Task<int> UpdateAsync(this IDbConnection connection, DynamicParameters parameters, string[] keyNames, string tableName, IDbTransaction transaction = null)
        {
            var template = CreateUpdateTemplate(connection, parameters, keyNames, tableName);
            return connection.ExecuteAsync(template.RawSql, parameters, transaction);
        }

        private static SqlBuilder.Template CreateUpdateTemplate(IDbConnection connection, DynamicParameters parameters, string[] keyNames, string tableName)
        {
            var sqlBuilder = new SqlBuilder();
            foreach (string name in parameters.ParameterNames)
            {
                // 是否为主键/条件参数
                if (keyNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    // 添加到 where 参数
                    sqlBuilder.Where($"{name}={connection.GetParameterName(name)}");
                }
                else
                {
                    // set 字段值
                    sqlBuilder.Set($"{name}={connection.GetParameterName(name)}");
                }
            }
            return sqlBuilder.AddTemplate($"update {tableName} /**set**/ /**where**/", parameters);
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">更新的数据</param>
        /// <param name="keyNames">主键名称</param>
        /// <param name="tableName">要添加的表</param>
        /// <param name="transaction">事务</param>
        /// <param name="exclude">需要排除的属性</param>
        /// <returns></returns>
        public static int Update(this IDbConnection connection, object obj, string[] keyNames, string tableName = null, IDbTransaction transaction = null, params string[] exclude)
        {
            return Update(connection, CreateDynamicParameters(obj, keyNames, exclude), keyNames, tableName ?? obj.GetType().Name, transaction);
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="obj">更新的数据</param>
        /// <param name="keyNames">主键名称</param>
        /// <param name="tableName">要添加的表</param>
        /// <param name="transaction">事务</param>
        /// <param name="exclude">需要排除的属性</param>
        /// <returns></returns>
        public static Task<int> UpdateAsync(this IDbConnection connection, object obj, string[] keyNames, string tableName = null, IDbTransaction transaction = null, params string[] exclude)
        {
            return UpdateAsync(connection, CreateDynamicParameters(obj, keyNames, exclude), keyNames, tableName ?? obj.GetType().Name, transaction);
        }

        /// <summary>
        /// 通过反射获取实例的属性
        /// </summary>
        /// <param name="type">实例的类型</param>
        /// <returns>实例的属性数组</returns>
        private static PropertyInfo[] GetProperties(Type type)
        {
            // 如果存在缓存则从缓存返回
            if (propertiesCaches.TryGetValue(type, out PropertyInfo[] values))
            {
                return values;
            }
            // 没有缓存，则先添加到缓存中再从缓存中返回
            propertiesCaches.Add(type, type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetGetMethod(false) != null)
                .ToArray());
            return propertiesCaches[type];
        }

        /// <summary>
        /// 获取参数化的名称
        /// </summary>
        /// <param name="connection">db connection</param>
        /// <param name="name">实例属性名</param>
        /// <returns>数据库参数化的名称</returns>
        public static string GetParameterName(this IDbConnection connection, string name)
        {
            // db connection 为 odbc
            if (connection.GetType().Name == "OdbcConnection")
            {
                return $"?{name}?";
            }
            return $"@{name}";
        }
    }
}
