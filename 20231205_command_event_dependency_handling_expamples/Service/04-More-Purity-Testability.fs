module Service.MorePurity


open System.Threading.Tasks
open Dependencies.Repositories
open Domain
open Domain.MorePurity
open Domain.FsStyleState
 

type IInvoiceRepository =
    abstract member GetInvoice : invoiceId:string -> Task<InvoiceState option>
    abstract member StoreInvoice : InvoiceState -> Task<unit>


type IInvoiceService =
    abstract member ExecuteInvoiceCommand : command:Command -> Task<unit>



let private executeCommand
    (invoiceRepo:IInvoiceRepository)
    (customerRepo:ICustomerRepository)
    (productRepo:IProductRepository)
    (command:Command) =
    task {
        
        let! invoice = invoiceRepo.GetInvoice command.InvoiceId
        
        // build the dependencies record based on the incoming commands
        let! dependencies =
            task {
                match command with
                | Command.CreateInvoice cmd ->
                    let! customer = customerRepo.GetCustomerById cmd.CustomerId
                    return { Dependencies.Empty with NeededCustomerForCreateInvoice = customer }
                | Command.AddInvoiceLine cmd ->
                    let! product = productRepo.GetProductById cmd.ProductId
                    return { Dependencies.Empty with NeededProductForAddInvoiceLine = product }
            }
        
        // this one it pure and perfectly testable    
        let result = invoice |> execute dependencies command
        
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


