#r "../../bin/Fsharp.Data.SqlClient.dll"
#r "Microsoft.SqlServer.Types.dll"
#r @"..\..\packages\FSharp.Configuration.0.5.3\lib\net40\FSharp.Configuration.dll"
//#load "ConnectionStrings.fs"
open System
open System.Data
open FSharp.Data


//[<Literal>] 
//let connectionString = ConnectionStrings.AdventureWorksLiteral
//let connectionString = ConnectionStrings.AdventureWorksAzure

//[<Literal>] 
//let prodConnectionString = ConnectionStrings.MasterDb

//type AdventureWorks = SqlProgrammabilityProvider<"Data Source=.;Initial Catalog = AdventureWorks2014;Integrated Security=True">
//type dbo = AdventureWorks.dbo

//let cmd = new SqlCommandProvider<"
//    SELECT X.* 
//    FROM Sales.SpecialOfferProduct X
//	    JOIN Sales.SalesOrderDetail Y ON X.ProductID = Y.ProductID 
//    WHERE X.ProductID = @specialOfferProductProductid 
//	    AND Y.ProductID = @salesOrderDetailProductid
//	    AND (X.SpecialOfferID IS NOT NULL 
//		    OR Y.SpecialOfferID IS NOT NULL)
//	     ", "Data Source=.;Initial Catalog = AdventureWorks2014;Integrated Security=True">()

type Thermion = SqlProgrammabilityProvider<"Data Source=.;Initial Catalog = SEN-QA-2015-12-10-11-13;Integrated Security=True">
let t = new Thermion.Data.Tables.WorkoverDetails()
t.rowIDColumn