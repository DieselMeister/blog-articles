module Tests.ClassicOOP


open Dependencies.DataTypes
open Domain
open Domain.ClassicOop
open Service.ClassicOop
open Dependencies.Repositories
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
     
        
[<Fact>]
let ``Test if OOP applies event data`` () =
    task {
        
        let customerRepo:ICustomerRepository =
            {
                new ICustomerRepository with
                    member this.GetCustomerById customerId =
                        task { return Some testCustomer }
            }
            
        let productRepo:IProductRepository =
            { new IProductRepository with
                    member this.GetProductById productId =
                        task { return Some testProduct }
            }
            
        let invoice = InvoiceAggregateRoot(AggregateId "INV001",customerRepo,productRepo)
        let createInvoiceCommand = Domain.Command.CreateInvoice { InvoiceId = "INV001"; CustomerId = "CUST001" }
        do! invoice.CreateInvoice createInvoiceCommand
        let (CustomerName name) = invoice.CustomerName
        Assert.Equal(testCustomer.Name, name)
        let (CustomerStreet street) = invoice.CustomerStreet
        Assert.Equal(testCustomer.Address.Street, street)
        return ()
        
    }

