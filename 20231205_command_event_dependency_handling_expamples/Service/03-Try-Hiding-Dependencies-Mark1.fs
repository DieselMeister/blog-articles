module Service.TryHidingMark1


open System.Threading.Tasks
open Domain
open Domain.TryHidingMark1
open Domain.FsStyleState
 

type IInvoiceRepository =
    abstract member GetInvoice : invoiceId:string -> Task<InvoiceState option>
    abstract member StoreInvoice : InvoiceState -> Task<unit>


type IInvoiceService =
    abstract member ExecuteInvoiceCommand : command:Command -> Task<unit>


let private executeCommand
    (invoiceRepo:IInvoiceRepository)
    (dependencies:Dependencies)
    (command:Command) =
    task {
        // partial application of the execute function
        let executeCommand = execute dependencies
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
    
    
    

type InvoiceService(
    invoiceRepo:IInvoiceRepository,
    dependencies:Dependencies) =
    interface IInvoiceService with
        member this.ExecuteInvoiceCommand command =
            executeCommand invoiceRepo dependencies command


