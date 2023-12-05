namespace Service

open System.Threading.Tasks

module ClassicOop =

    open System    
    open Dependencies
    open Dependencies.Repositories    
    open Domain
    open Domain.ClassicOop
    open DataTypes
    
    type IInvoiceService =
        abstract member GetInvoice : invoiceId:string -> Task<InvoiceAggregateRoot>
        abstract member StoreInvoice : InvoiceAggregateRoot -> Task<unit>
    
    type DomainService(
        invoiceService: IInvoiceService,
        customerRepo: ICustomerRepository,
        productRepo: IProductRepository
        ) =
        
        member this.CreateInvoice invoiceId customerId =
            task {
                let aggregateId = AggregateId (invoiceId)
                let invoiceAggregate = InvoiceAggregateRoot(aggregateId, customerRepo, productRepo)
                let command = Command.CreateInvoice { InvoiceId = invoiceId; CustomerId = customerId }
                do! invoiceAggregate.CreateInvoice command
                do! invoiceService.StoreInvoice invoiceAggregate
            }
            
            
        member this.AddInvoiceLine invoiceId productId quantity =
            task {
                let! invoiceAggregate = invoiceService.GetInvoice invoiceId
                let command = Command.AddInvoiceLine { InvoiceId = invoiceId; ProductId = productId; Quantity = quantity }
                do! invoiceAggregate.AddInvoiceLine command
                do! invoiceService.StoreInvoice invoiceAggregate
            }