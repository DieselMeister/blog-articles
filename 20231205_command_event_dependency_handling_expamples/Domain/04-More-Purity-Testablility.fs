namespace Domain

module MorePurity =


    open System.Threading.Tasks
    open Dependencies
    open FsStyleState

    
    type Dependencies = {
        NeededCustomerForCreateInvoice : DataTypes.Customer option
        NeededProductForAddInvoiceLine  : DataTypes.Product  option
    } with
        static member Empty = {
            NeededCustomerForCreateInvoice = None
            NeededProductForAddInvoiceLine  = None
        }

    // execute a command (decider)
    let execute dependencies command state =
        match state, command with
        | None, Command.CreateInvoice cmd ->
            match dependencies.NeededCustomerForCreateInvoice with
            | None ->
                Error $"customer '{cmd.CustomerId}' does not exist"
            | Some customer ->
                [
                    Event.InvoiceCreated {
                        InvoiceId      = InvoiceId cmd.InvoiceId
                        CustomerId     = CustomerId cmd.CustomerId
                        CustomerName   = CustomerName customer.Name
                        CustomerStreet = CustomerStreet customer.Address.Street
                        CustomerCity   = CustomerCity customer.Address.City
                    }
                ] |> Ok
            
        | Some state, Command.AddInvoiceLine cmd ->
            match dependencies.NeededProductForAddInvoiceLine with
            | None ->
                Error $"product '{cmd.ProductId}' does not exist"
            | Some product ->
                [
                    Event.InvoiceLineAdded {
                        InvoiceId        = state.InvoiceId
                        ProductId        = ProductId cmd.ProductId
                        ProductName      = ProductName product.Name
                        ProductPrice     = ProductPrice product.Price
                        ProductQuantity  = ProductQuantity cmd.Quantity
                        TotalPrice       = TotalPrice (product.Price * decimal cmd.Quantity)
                    }
                ] |> Ok
            
        | Some state, Command.CreateInvoice _ ->
            Error "invoice already exists"
        | None, Command.AddInvoiceLine _ ->
            Error "invoice does not exist"
        
        
    // apply an event (state transition)
    let apply event state =
        match event, state with
        | Event.InvoiceCreated ev, None ->
            Some {
                InvoiceId      = ev.InvoiceId
                CustomerId     = ev.CustomerId
                CustomerName   = ev.CustomerName
                CustomerStreet = ev.CustomerStreet
                CustomerCity   = ev.CustomerCity
                InvoiceLines   = []
            }
        | Event.InvoiceLineAdded ev, Some state ->
            Some {
                state with
                    InvoiceLines = {
                        ProductId        = ev.ProductId
                        ProductName      = ev.ProductName
                        ProductPrice     = ev.ProductPrice
                        ProductQuantity  = ev.ProductQuantity
                        TotalPrice       = ev.TotalPrice
                    } :: state.InvoiceLines
            }
        | _, _ ->
            None
            
            
    let applyEvents events state =
        (events, state)
        ||> List.fold (fun state event -> apply event state)