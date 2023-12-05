namespace Domain

open Domain

module ClassicOop =
    
    open Dependencies.DataTypes
    open Dependencies.Repositories
    open Domain
    
    type AggregateId = AggregateId of int

        
    type IAggregate =
        abstract member Id : AggregateId
        
        
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
        
        interface IAggregate with
            member _.Id = id
        
        // the properties (state)
        member _.InvoiceId      with get() = invoiceId and private set v = invoiceId <- v 
        member _.CustomerId     with get() = customerId and private set v = customerId <- v
        member _.CustomerName   with get() = customerName and private set v = customerName <- v
        member _.CustomerStreet with get() = customerStreet and private set v = customerStreet <- v
        member _.CustomerCity   with get() = customerCity and private set v = customerCity <- v
        
        member _.InvoiceLines with get() = invoiceLines and private set v = invoiceLines <- v
        
        // the event list
        member val Events = events with get
        
        // the methods
        member _.CreateInvoice(Command.CreateInvoice cmd) =
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
                    
            }
        
        
        member _.AddInvoiceLine (Command.AddInvoiceLine cmd) =
            task {
                let! product = productRepo.GetProductById cmd.ProductId
                match product with
                | None -> failwith "invalid product id"
                | Some product ->
                    let invoiceLine = InvoiceLineAggregate (AggregateId 0, productRepo)
                    do! invoiceLine.AddInvoiceLine cmd
                    invoiceLines <- invoiceLine :: invoiceLines
            }
            
            
        member _.Apply(event) =
            match event with
            | Event.InvoiceCreated event ->
                invoiceId       <- event.InvoiceId
                customerId      <- event.CustomerId
                customerName    <- event.CustomerName
                customerStreet  <- event.CustomerStreet
                customerCity    <- event.CustomerCity
        
        
    and InvoiceLineAggregate(
        id : AggregateId,
        productRepo: IProductRepository) =
            
        // the fields
        let mutable productId = ProductId ""
        let mutable productName = ProductName ""
        let mutable productPrice = ProductPrice 0.0m
        let mutable productQuantity = ProductQuantity 0
        let mutable totalPrice = TotalPrice 0.0m
        
        interface IAggregate with
            member _.Id = id        
            
        // the properties
        member _.ProductId      with get() = productId and private set v = productId <- v
        member _.ProductName    with get() = productName and private set v = productName <- v
        member _.ProductPrice   with get() = productPrice and private set v = productPrice <- v
        member _.ProductQuantity with get() = productQuantity and private set v = productQuantity <- v
        member _.TotalPrice     with get() = totalPrice and private set v = totalPrice <- v
        
        // the methods
        
        
        
        member _.Apply (Event.InvoiceLineAdded event) =
            task {
                
                match product with
                | None -> failwith "invalid product id"
                | Some product ->
                    productId       <- ProductId product.ProductId
                    productName     <- ProductName product.Name
                    productPrice    <- ProductPrice product.Price
                    productQuantity <- ProductQuantity cmd.Quantity
                    totalPrice      <- TotalPrice (product.Price * decimal cmd.Quantity)
            }
                
            
        
        
            
        
            