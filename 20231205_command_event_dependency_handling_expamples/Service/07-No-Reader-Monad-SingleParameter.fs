module Service.NoReaderMonadButSingleParameter

open System.Threading.Tasks
open Dependencies.Repositories
open Domain.TryHidingMark2
open Domain.FsStyleState
 

type IInvoiceRepository =
    abstract member GetInvoice : invoiceId:string -> Task<InvoiceState option>
    abstract member StoreInvoice : InvoiceState -> Task<unit>


type IInvoiceService =
    abstract member ExecuteInvoiceCommand : command:ExternalCommand -> Task<unit>


type IDependencies =
    inherit IInvoiceRepository
    inherit ICustomerRepository
    inherit IProductRepository


let private executeCommand
    (env:IDependencies)
    (command:ExternalCommand) =
    task {
        let! invoice = env.GetInvoice command.InvoiceId
        // build the internal commands
        let! internalCommand =
            task {
                match command with
                | ExternalCommand.CreateInvoice cmd ->
                    let! customer = env.GetCustomerById cmd.CustomerId
                    return Command.CreateInvoice {
                        InvoiceId = cmd.InvoiceId
                        CustomerId = cmd.CustomerId
                        Customer = customer
                    }
                | ExternalCommand.AddInvoiceLine cmd ->
                    let! product = env.GetProductById cmd.ProductId
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
                do! env.StoreInvoice newInvoiceState
    }
    
    
    
type InvoiceService(dependencies:IDependencies) =
    interface IInvoiceService with
        member this.ExecuteInvoiceCommand command =
            executeCommand dependencies command
           



