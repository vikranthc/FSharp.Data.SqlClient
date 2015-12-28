﻿namespace FSharp.Data.SqlClient

open System
open System.Data
open System.Data.SqlClient
open System.Reflection
open System.Collections
open System.Diagnostics

open Microsoft.FSharp.Quotations

open FSharp.Data

type QuotationsFactory private() = 
    
    static member internal GetBody(methodName, specialization, [<ParamArray>] bodyFactoryArgs : obj[]) =
        
        let bodyFactory =   
            let mi = typeof<QuotationsFactory>.GetMethod(methodName, BindingFlags.NonPublic ||| BindingFlags.Static)
            assert(mi <> null)
            mi.MakeGenericMethod([| specialization |])

        fun(args : Expr list) -> 
            let parameters = Array.append [| box args |] bodyFactoryArgs
            bodyFactory.Invoke(null, parameters) |> unbox

    static member internal ToSqlParam(p : Parameter) = 

        let tvpColumnNames, tvpColumnTypes = 
            if not p.TypeInfo.TableType 
            then [], []
            else [ for c in p.TypeInfo.TableTypeColumns.Value -> c.Name, c.TypeInfo.ClrType.FullName ] |> List.unzip

        let name = p.Name
        let sqlDbType = p.TypeInfo.SqlDbType
        let isFixedLength = p.TypeInfo.IsFixedLength

        <@@ 
            let x = SqlParameter(name, sqlDbType, Direction = %%Expr.Value p.Direction)

            if not isFixedLength then x.Size <- %%Expr.Value p.Size 

            x.Precision <- %%Expr.Value p.Precision
            x.Scale <- %%Expr.Value p.Scale

            if x.SqlDbType = SqlDbType.Structured
            then 
                let typeName: string = sprintf "%s.%s" (%%Expr.Value p.TypeInfo.Schema) (%%Expr.Value p.TypeInfo.UdttName)
                //done via reflection because not implemented on Mono
                x.GetType().GetProperty("TypeName").SetValue(x, typeName, null)

            if %%Expr.Value p.TypeInfo.SqlEngineTypeId = 240 
            then
                x.UdtTypeName <- %%Expr.Value p.TypeInfo.TypeName
            
            if not tvpColumnNames.IsEmpty
            then 
                let table = new DataTable()
                for name, typeName in List.zip tvpColumnNames tvpColumnTypes do
                    let c = new DataColumn(name, Type.GetType( typeName, throwOnError = true))
                    table.Columns.Add c
                x.Value <- table
            x
        @@>

    static member internal OptionToObj<'T> value = <@@ match %%value with Some (x : 'T) -> box x | None -> Extensions.DbNull @@>    
        
    static member internal MapArrayOptionItemToObj<'T>(arr, index) =
        <@
            let values : obj[] = %%arr
            values.[index] <- match unbox values.[index] with Some (x : 'T) -> box x | None -> null 
        @> 

    static member internal MapArrayObjItemToOption<'T>(arr, index) =
        <@
            let values : obj[] = %%arr
            values.[index] <- box <| if Convert.IsDBNull(values.[index]) then None else Some(unbox<'T> values.[index])
        @> 

    static member internal MapArrayNullableItems(outputColumns : Column list, mapper : string) = 
        let columnTypes, isNullableColumn = outputColumns |> List.map (fun c -> c.TypeInfo.ClrTypeFullName, c.Nullable) |> List.unzip
        QuotationsFactory.MapArrayNullableItems(columnTypes, isNullableColumn, mapper)            

    static member internal MapArrayNullableItems(columnTypes : string list, isNullableColumn : bool list, mapper : string) = 
        assert(columnTypes.Length = isNullableColumn.Length)
        let arr = Var("_", typeof<obj[]>)
        let body =
            (columnTypes, isNullableColumn) 
            ||> List.zip
            |> List.mapi(fun index (typeName, isNullableColumn) ->
                if isNullableColumn 
                then 
                    typeof<QuotationsFactory>
                        .GetMethod(mapper, BindingFlags.NonPublic ||| BindingFlags.Static)
                        .MakeGenericMethod( Type.GetType( typeName, throwOnError = true))
                        .Invoke(null, [| box(Expr.Var arr); box index |])
                        |> unbox
                        |> Some
                else 
                    None
            ) 
            |> List.choose id
            |> List.fold (fun acc x ->
                Expr.Sequential(acc, x)
            ) <@@ () @@>
        Expr.Lambda(arr, body)

    static member internal GetNullableValueFromDataRow<'T>(exprArgs : Expr list, name : string) =
        <@
            let row : DataRow = %%exprArgs.[0]
            if row.IsNull name then None else Some(unbox<'T> row.[name])
        @> 

    static member internal SetNullableValueInDataRow<'T>(exprArgs : Expr list, name : string) =
        <@
            (%%exprArgs.[0] : DataRow).[name] <- match (%%exprArgs.[1] : option<'T>) with None -> DbNull | Some value -> box value
        @> 

    static member GetMapperWithNullsToOptions(nullsToOptions, mapper: obj[] -> obj) = 
        fun values -> 
            nullsToOptions values
            mapper values