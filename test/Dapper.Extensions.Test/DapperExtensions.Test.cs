using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Odbc;

namespace Dapper.Extensions.Test
{
    [TestClass]
    public class DapperExtensionsTest
    {
        public static OdbcConnection CreateOdbcConnection()
        {
            return new OdbcConnection("Driver={ODBC Driver 17 for SQL Server}; Server=(LocalDb)\\MSSQLLocalDB; Database=test_db;Integrated Security=True");
        }

        public static SqlConnection CreateSqlConnection()
        {
            return new SqlConnection("Data Source=(LocalDb)\\MSSQLLocalDB;Initial Catalog=test_db;Integrated Security=True");
        }

        private OdbcConnection odbcConnection = null;
        private SqlConnection sqlConnection = null;

        [TestInitialize]
        public void Init()
        {
            odbcConnection = CreateOdbcConnection();
            sqlConnection = CreateSqlConnection();
        }

        [TestCleanup]
        public void Cleanup()
        {
            odbcConnection.Dispose();
            sqlConnection.Dispose();
        }

        [TestMethod]
        public void OdbcTest()
        {
            var entry = new TestEntry { Name = "Odbc name1", Value = "Odbc value1", Now = DateTime.Now };
            // insert
            entry.Id = odbcConnection.Save(entry, new string[] { "Id" });
            // 是否已添加
            bool exists = odbcConnection.CheckExists(entry, new string[] { "Id" });

            Assert.IsTrue(exists);

            // update 更新字段
            entry.Name = "Odbc updated name";
            odbcConnection.Save(entry, new string[] { "Id" });

            var found = odbcConnection.QueryFirstOrDefault<TestEntry>($"select * from TestEntry where Id = {odbcConnection.GetParameterName("Id")}", entry);

            Assert.IsNotNull(found);
            Assert.AreEqual(entry.Name, found.Name);
            Assert.AreEqual(entry.Value, found.Value);

            var id = odbcConnection.Insert(new TestEntry { Name = "Odbc name2", Value = "Odbc value2", Now = DateTime.Now }, exclude: "Id");
            var count = odbcConnection.Update(entry, new string[] { "Id" });

            Assert.IsNotNull(id);
            Assert.IsTrue(count > 0);
        }

        [TestMethod]
        public void SqlTest()
        {
            var entry = new TestEntry { Name = "MS SQL name1", Value = "MS SQL value1", Now = DateTime.Now };
            entry.Id = sqlConnection.Save(entry, new string[] { "Id" });

            bool Exists = sqlConnection.CheckExists(entry, new string[] { "Id" });

            Assert.IsTrue(Exists);

            entry.Name = "MS SQL updated name";

            sqlConnection.Save(entry, new string[] { "Id" });

            var found = sqlConnection.QueryFirstOrDefault<TestEntry>($"select * from TestEntry where Id = {sqlConnection.GetParameterName("Id")}", entry);

            Assert.IsNotNull(found);
            Assert.AreEqual(entry.Name, found.Name);
            Assert.AreEqual(entry.Value, found.Value);

            var id = sqlConnection.Insert(new TestEntry { Name = "MS SQL name2", Value = "MS SQL value2", Now = DateTime.Now }, exclude: "Id");
            var count = sqlConnection.Update(entry, new string[] { "Id" });

            Assert.IsNotNull(id);
            Assert.IsTrue(count > 0);
        }

        [TestMethod]
        public void OdbcDateTimeOverflowFixTest()
        {
            var entry = new TestEntry { Name = "name1", Value = "value1", Now = DateTime.Now };
            entry.Id = odbcConnection.Save(entry, new string[] { "Id" });

            Assert.IsNotNull(entry.Id);
        }

        [TestMethod]
        public void SqlDateTimeOverflowFixTest()
        {
            var entry = new TestEntry { Name = "name1", Value = "value1", Now = DateTime.Now };
            entry.Id = sqlConnection.Save(entry, new string[] { "Id" });

            Assert.IsNotNull(entry.Id);
        }

        public class TestEntry
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
            public DateTime? Now { get; set; }
        }
    }
}