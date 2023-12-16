namespace Domain

open Domain

module ClassicOop =
    
    open System
    open Dependencies.DataTypes
    open Dependencies.Repositories
    open Domain
    
    type AggregateId = AggregateId of string  

        
    type IAggregate =
        abstract member Id : AggregateId
        
    type IAggregateRoot =
        inherit IAggregate
        
    type InvoiceAggregateRoot(
        id : AggregateId,
        customerRepo: ICustomerRepository,
        productRepo: IProductRepository) =
        
        // the fields
        let mutable invoiceId = InvoiceId ""
        let mutable customerId = CustomerId ""
        let mutable customerName = CustomerName ""
        let mutable customerStreet = CustomerStreet ""
        let mutable customerCity = CustomerCity ""
        let mutable invoiceLines: InvoiceLineAggregate list = []
        
        let mutable events: Event list = []
        
        interface IAggregateRoot with
            member _.Id = id
        
        // the properties (state)
        member _.InvoiceId        with get() = invoiceId      
        member _.CustomerId       with get() = customerId    
        member _.CustomerName     with get() = customerName  
        member _.CustomerStreet   with get() = customerStreet
        member _.CustomerCity     with get() = customerCity  
        
        member _.InvoiceLines     with get() = invoiceLines 
        
        // the event list
        member _.Events           with get() = events
        
        
        // the methods
        member this.CreateInvoice(Command.CreateInvoice cmd) =
            task {
                let! customer = customerRepo.GetCustomerById cmd.CustomerId
                match customer with
                | None -> failwith "invalid customer id"
                | Some customer ->
                    let newEvent =
                        Event.InvoiceCreated {
                            InvoiceId       = InvoiceId cmd.InvoiceId
                            CustomerId      = CustomerId customer.CustomerId
                            CustomerName    = CustomerName customer.Name
                            CustomerStreet  = CustomerStreet customer.Address.Street
                            CustomerCity    = CustomerCity customer.Address.City        
                        }
                    events <- newEvent :: events
                    this.Apply newEvent
            }
        
        
        member this.AddInvoiceLine (Command.AddInvoiceLine cmd) =
            task {
                let! product = productRepo.GetProductById cmd.ProductId
                match product with
                | None -> failwith "invalid product id"
                | Some product ->
                    let newEvent =
                        Event.InvoiceLineAdded {
                            InvoiceId       = InvoiceId cmd.InvoiceId
                            ProductId       = ProductId cmd.ProductId
                            ProductName     = ProductName product.Name
                            ProductPrice    = ProductPrice product.Price
                            ProductQuantity = ProductQuantity cmd.Quantity
                            TotalPrice      = TotalPrice (product.Price * decimal cmd.Quantity)
                        }
                    events <- newEvent :: events
                    this.Apply newEvent
            }
            
            
        member this.Apply(event) =
            match event with
            | Event.InvoiceCreated ev ->
                invoiceId       <- ev.InvoiceId
                customerId      <- ev.CustomerId
                customerName    <- ev.CustomerName
                customerStreet  <- ev.CustomerStreet
                customerCity    <- ev.CustomerCity
                
            | Event.InvoiceLineAdded ev ->
                let invoiceLine = InvoiceLineAggregate(AggregateId <| Guid.NewGuid().ToString())
                invoiceLine.Apply event
                invoiceLines <- invoiceLine :: invoiceLines
                
                
        member this.ApplyEvents (events: Event list) =
            events |> List.iter (fun event -> this.Apply event)
        
        
    and InvoiceLineAggregate(
        id : AggregateId) =
            
        // the fields
        let mutable productId = ProductId ""
        let mutable productName = ProductName ""
        let mutable productPrice = ProductPrice 0.0m
        let mutable productQuantity = ProductQuantity 0
        let mutable totalPrice = TotalPrice 0.0m
        
        interface IAggregate with
            member _.Id = id        
            
        // the properties
        member _.ProductId          with get() = productId       
        member _.ProductName        with get() = productName     
        member _.ProductPrice       with get() = productPrice    
        member _.ProductQuantity    with get() = productQuantity 
        member _.TotalPrice         with get() = totalPrice      
        
        // the methods
        
        member _.Apply (event:Event) =
            match event with
            | Event.InvoiceLineAdded event ->
                productId       <- event.ProductId
                productName     <- event.ProductName
                productPrice    <- event.ProductPrice
                productQuantity <- event.ProductQuantity
                totalPrice      <- event.TotalPrice
            | _ ->
                failwith "invalid event"
                
            
        
        
            
        
            