module Tests.TestsAfterSuccessfullyHiding

open Dependencies.DataTypes
open Domain
open Domain.FsStyleState
open Domain.TryHidingMark2
open Xunit

let testCustomer : Customer = {
    CustomerId = "CUST001"
    Name = "Test Customer"
    Address = { Street = "Test Street"; City = "Test City" }
}


let testProduct : Product = {
    ProductId = "PROD1"
    Name = "Product1"
    Price = 10.0m
}


let testInvoice : InvoiceState = {
    InvoiceId = InvoiceId "INV001"
    CustomerId = CustomerId "CUST001"
    CustomerName = CustomerName "Test Customer"
    CustomerStreet = CustomerStreet "Test Street"
    CustomerCity = CustomerCity "Test City"
    InvoiceLines = []
}


[<Fact>]
let ``Should handle CreateInvoice command successfully when customer exists`` () =
    let initialState = None
    let testCommand = Command.CreateInvoice {
        InvoiceId = "INV001"
        CustomerId = "CUST001"
        Customer = Some testCustomer
    }
    let result = execute testCommand initialState
    match result with
    | Ok events ->
        let expectedEvent = Domain.Event.InvoiceCreated {
            InvoiceId = InvoiceId "INV001";
            CustomerId = CustomerId "CUST001";
            CustomerName = CustomerName "Test Customer";
            CustomerStreet = CustomerStreet "Test Street";
            CustomerCity = CustomerCity "Test City";
        }
        Assert.Equal(1, List.length events)
        Assert.Equal(expectedEvent, List.head events)
    | Error _ ->
        Assert.True(false, "Unexpected error result")
        
        

[<Fact>]
let ``Should return error on CreateInvoice when customer is missing`` () =
    let initialState = None
    let testCommand = Command.CreateInvoice {
        InvoiceId = "INV001";
        CustomerId = "CUST001";
        Customer = None
    }
    let result = execute testCommand initialState
    match result with
    | Ok _ ->
        Assert.True(false, "Unexpected success result")
    | Error msg ->
        Assert.Equal($"customer 'CUST001' does not exist", msg)
        
        
        
[<Fact>]
let ``Should return error on CreateInvoice when customer already exisits`` () =
    let initialState = Some testInvoice
    let testCommand = Command.CreateInvoice {
        InvoiceId = "INV001";
        CustomerId = "CUST001";
        Customer = None
    }
    let result = execute testCommand initialState
    match result with
    | Ok _ ->
        Assert.True(false, "Unexpected success result")
    | Error msg ->
        Assert.Equal($"invoice 'INV001' already exists", msg)        


[<Fact>]
let ``Should handle AddInvoiceLine successfully, when product is available`` () =
    let initialState = Some testInvoice
    let testCommand = Command.AddInvoiceLine {
        InvoiceId = "INV001"
        ProductId = "PROD1"
        Quantity = 3
        Product = Some testProduct
    }
    let result = execute testCommand initialState
    match result with
    | Ok events ->
        let expectedEvent = Domain.Event.InvoiceLineAdded {
            InvoiceId = InvoiceId "INV001"
            ProductId = ProductId "PROD1"
            ProductName= ProductName testProduct.Name
            ProductPrice= ProductPrice testProduct.Price
            ProductQuantity= ProductQuantity 3
            TotalPrice= TotalPrice (testProduct.Price * 3.0m)
        }
        Assert.Equal(1, List.length events)
        Assert.Equal(expectedEvent, List.head events)
    | Error _ ->
        Assert.True(false, "Unexpected error result")
        
        
[<Fact>]
let ``Should return error on AddInvoiceLine, when product is missing`` () =
    let initialState = Some testInvoice
    let testCommand = Command.AddInvoiceLine {
        InvoiceId = "INV001"
        ProductId = "PROD1"
        Quantity = 3
        Product = None
    }
    let result = execute testCommand initialState
    match result with
    | Ok _ ->
        Assert.True(false, "Unexpected success result")
    | Error msg ->
        Assert.Equal($"product 'PROD1' does not exist", msg)
        
        
[<Fact>]
let ``Should return error on AddInvoiceLine, when invoice is missing`` () =
    let initialState = None
    let testCommand = Command.AddInvoiceLine {
        InvoiceId = "INV001"
        ProductId = "PROD1"
        Quantity = 3
        Product = None
    }
    let result = execute testCommand initialState
    match result with
    | Ok _ ->
        Assert.True(false, "Unexpected success result")
    | Error msg ->
        Assert.Equal($"invoice does not exist", msg)
        
  

