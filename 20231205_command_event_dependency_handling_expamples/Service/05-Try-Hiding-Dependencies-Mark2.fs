module Service.TryHidingMark2

open System.Threading.Tasks
open Dependencies.Repositories
open Domain.TryHidingMark2
open Domain.FsStyleState
 

type IInvoiceRepository =
    abstract member GetInvoice : invoiceId:string -> Task<InvoiceState option>
    abstract member StoreInvoice : InvoiceState -> Task<unit>


type IInvoiceService =
    abstract member ExecuteInvoiceCommand : command:ExternalCommand -> Task<unit>


// btw for the repository dependencies here, you can also use a dependency record. Or move this directly into the service class
let private executeCommand
    (invoiceRepo:IInvoiceRepository)
    (customerRepo:ICustomerRepository)
    (productRepo:IProductRepository)
    (command:ExternalCommand) =
    task {
        
        let! invoice = invoiceRepo.GetInvoice command.InvoiceId
        
        // build the internal commands
        let! internalCommand =
            task {
                match command with
                | ExternalCommand.CreateInvoice cmd ->
                    let! customer = customerRepo.GetCustomerById cmd.CustomerId
                    return Command.CreateInvoice {
                        InvoiceId = cmd.InvoiceId
                        CustomerId = cmd.CustomerId
                        Customer = customer
                    }
                | ExternalCommand.AddInvoiceLine cmd ->
                    let! product = productRepo.GetProductById cmd.ProductId
                    return Command.AddInvoiceLine {
                        InvoiceId = cmd.InvoiceId
                        ProductId = cmd.ProductId
                        Product = product
                        Quantity = cmd.Quantity
                    }
            }
        
        let result = invoice |> execute internalCommand
        
        match result with
        | Error e -> failwith e
        | Ok events ->
            let newInvoiceState = applyEvents invoice events
            match newInvoiceState with
            | None -> failwith "no state returned after events applied"
            | Some newInvoiceState ->
                do! invoiceRepo.StoreInvoice newInvoiceState
    }
    
    

type InvoiceService(
    invoiceRepo:IInvoiceRepository,
    customerRepo:ICustomerRepository,
    productRepo:IProductRepository) =
    interface IInvoiceService with
        member this.ExecuteInvoiceCommand command =
            executeCommand invoiceRepo customerRepo productRepo command


