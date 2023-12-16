module Service.MoreIdomaticMark1

// if you want to be pragmatic and using dependency injection from asp.net core or similar
// things, you at can end up here with an object
// but to have at least some of the feeling of F#, you can use the object expression and
// create a factory function
// or use an object directly

open System.Threading.Tasks
open Dependencies.Repositories
open Domain
open Domain.MoreIdomatic
open Domain.FsStyleState

// somewhere we have our invoice repository, but here with a slightly different singature
type IInvoiceRepository =
    abstract member GetInvoice : invoiceId:string -> Task<InvoiceState option>
    abstract member StoreInvoice : InvoiceState -> Task<unit>

// i am using here my personal preferred way to have one execute function also in the service
type IInvoiceService =
    abstract member ExecuteInvoiceCommand : command:Command -> Task<unit>



let private executeCommand
    (invoiceRepo:IInvoiceRepository)
    (customerRepo:ICustomerRepository)
    (productRepo:IProductRepository)
    (command:Command) =
    task {
        // partial application of the execute function
        let executeCommand = execute customerRepo.GetCustomerById productRepo.GetProductById
        let! invoice = invoiceRepo.GetInvoice command.InvoiceId
        let! result = invoice |> executeCommand command
        match result with
        | Error e -> failwith e
        | Ok events ->
            let newInvoiceState = applyEvents invoice events
            match newInvoiceState with
            | None -> failwith "no state returned after events applied"
            | Some newInvoiceState ->
                do! invoiceRepo.StoreInvoice newInvoiceState
    }
    
    
    
let createInvoiceService (invoiceRepo:IInvoiceRepository) (customerRepo:ICustomerRepository) (productRepo:IProductRepository) =
    { new IInvoiceService with
        member this.ExecuteInvoiceCommand command =
            executeCommand invoiceRepo customerRepo productRepo command
    }
    
// or as an alternative
type InvoiceService(
    invoiceRepo:IInvoiceRepository,
    customerRepo:ICustomerRepository,
    productRepo:IProductRepository) =
    interface IInvoiceService with
        member this.ExecuteInvoiceCommand command =
            executeCommand invoiceRepo customerRepo productRepo command


