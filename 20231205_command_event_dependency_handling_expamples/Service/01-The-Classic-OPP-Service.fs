namespace Service

open System.Threading.Tasks

module ClassicOop =

    open Dependencies.Repositories    
    open Domain
    open Domain.ClassicOop
    
    type IInvoiceRepository =
        abstract member GetInvoice : invoiceId:string -> Task<InvoiceAggregateRoot option>
        abstract member StoreInvoice : InvoiceAggregateRoot -> Task<unit>
    
    
    type IInvoiceService =
        abstract member CreateInvoice : invoiceId:string -> customerId:string -> Task<unit>
        abstract member AddInvoiceLine : invoiceId:string -> productId:string -> quantity:int -> Task<unit>
        
     
    type IInvoiceServiceAlternative =
        abstract member ExecuteInvoiceCommand : command:Command -> Task<unit>
    
    
    type InvoiceService(
        invoiceRepo: IInvoiceRepository,
        customerRepo: ICustomerRepository,
        productRepo: IProductRepository
        ) =
        interface IInvoiceService with
        
            member this.CreateInvoice invoiceId customerId =
                task {
                    let! invoice = invoiceRepo.GetInvoice invoiceId
                    let aggregateId = AggregateId (invoiceId)
                    let invoiceAggregate = InvoiceAggregateRoot(aggregateId, customerRepo, productRepo)
                    let command = Command.CreateInvoice { InvoiceId = invoiceId; CustomerId = customerId }
                    do! invoiceAggregate.CreateInvoice command
                    do! invoiceRepo.StoreInvoice invoiceAggregate
                }
                
                
            member this.AddInvoiceLine invoiceId productId quantity =
                task {
                    let! invoiceAggregate = invoiceRepo.GetInvoice invoiceId
                    match invoiceAggregate with
                    | None -> failwith "Invoice not found"
                    | Some invoiceAggregate ->
                        let command = Command.AddInvoiceLine { InvoiceId = invoiceId; ProductId = productId; Quantity = quantity }
                        do! invoiceAggregate.AddInvoiceLine command
                        do! invoiceRepo.StoreInvoice invoiceAggregate
                }
                
                
    type InvoiceServiceAlternative(
        invoiceRepo: IInvoiceRepository,
        customerRepo: ICustomerRepository,
        productRepo: IProductRepository
        ) =
        interface IInvoiceServiceAlternative with
        
            member this.ExecuteInvoiceCommand command =
                task {
                    let! invoice = invoiceRepo.GetInvoice command.InvoiceId
                    match command with
                    | Command.CreateInvoice cmd ->
                        match invoice with
                        | None ->
                            let aggregateId = AggregateId (cmd.InvoiceId)
                            let invoiceAggregate = InvoiceAggregateRoot(aggregateId, customerRepo, productRepo)
                            do! invoiceAggregate.CreateInvoice command
                            do! invoiceRepo.StoreInvoice invoiceAggregate
                        | Some _ ->
                            failwith "Invoice already exists"
                            
                    | Command.AddInvoiceLine _ ->
                        match invoice with
                        | None -> failwith "Invoice not found"
                        | Some invoiceAggregate ->
                            do! invoiceAggregate.AddInvoiceLine command
                            do! invoiceRepo.StoreInvoice invoiceAggregate
                        
                }     