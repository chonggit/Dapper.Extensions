# Dapper Insert 及 Update 扩展方法

在 [Dapper.Rainbow](https://github.com/DapperLib/Dapper/tree/main/Dapper.Rainbow) 项目中，可以对 entity 及数据库表做简单的映射并对 entity 执行 CRUD 操作。但在使用 OdbcConnection 时，OdbcConnection 使用`?`作为参数占位符，没有使用名命参数占位符，Dapper.Rainbow 暂未对此支持。