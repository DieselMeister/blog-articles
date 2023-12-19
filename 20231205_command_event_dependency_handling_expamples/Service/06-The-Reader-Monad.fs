module Service.TheReaderMonad

open System.Threading.Tasks
open Dependencies.Repositories
open Domain.TryHidingMark2
open Domain.FsStyleState
 

type IInvoiceRepository =
    abstract member GetInvoice : invoiceId:string -> Task<InvoiceState option>
    abstract member StoreInvoice : InvoiceState -> Task<unit>


type IInvoiceService =
    abstract member ExecuteInvoiceCommand : command:ExternalCommand -> Task<unit>






// Implementation of the Reader Monad (called Dependency here)
// source: https://www.bartoszsypytkowski.com/dealing-with-complex-dependency-injection-in-f/
// source: https://fsharpforfunandprofit.com/posts/dependencies-3/
[<AutoOpen>]
module TheReaderMonad =    
    [<Struct>] type Dependency<'env, 'out> = Dependency of ('env -> 'out)
    module Dependency =
        /// Create value with no dependency requirements.
        let inline value (x: 'out): Dependency<'env,'out> = Dependency (fun _ -> x)
        /// Create value which uses dependency.
        let inline apply (fn: 'env -> 'out): Dependency<'env,'out> = Dependency fn
        
        let run (env: 'env) (Dependency fn): 'out = fn env
        
        let inline bind (fn: 'a -> Dependency<'env,'b>) effect =
            Dependency (fun env ->
                let x = run env effect // compute result of the first effect
                run env (fn x) // run second effect, based on result of first one
            )
            
        let inline get<'a> = Dependency id
            
    [<Struct>]
    type DependencyBuilder =
        member inline __.Return value = Dependency.value value
        member inline __.Zero () = Dependency.value (Unchecked.defaultof<_>)
        member inline __.ReturnFrom (effect: Dependency<'env, 'out>) = effect
        member inline __.Bind(effect, fn) = Dependency.bind fn effect
        
    let dependency = DependencyBuilder()


type IDependencies =
    inherit IInvoiceRepository
    inherit ICustomerRepository
    inherit IProductRepository 


// here we use instead of normal parameter injection, a reader monad (called dependency here)
let private executeCommand (command:ExternalCommand) =
    dependency {
        let! (env:IDependencies) = Dependency.get
        
        return task {
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
    }
    
    
   
    
// consider this more of an service on api level
// like an http handler or controller or whatever
// an not any more as application service
type InvoiceService(dependencies:IDependencies) =
    interface IInvoiceService with
        member this.ExecuteInvoiceCommand command =
            Dependency.run dependencies (executeCommand command)
           



