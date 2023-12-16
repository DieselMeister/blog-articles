namespace Domain

module TryHidingMark1 =


    open System.Threading.Tasks
    open Dependencies
    open FsStyleState

    type Dependencies = {
        GetCustomer : string -> Task<DataTypes.Customer option>
        GetProduct  : string  -> Task<DataTypes.Product  option>
    }

    // execute a command (decider)
    let execute dependencies command state =
        task {
            match state, command with
            | None, Command.CreateInvoice cmd ->
                let! (customer:DataTypes.Customer option) = dependencies.GetCustomer cmd.CustomerId
                match customer with
                | None ->
                    return Error $"customer '{cmd.CustomerId}' does not exist"
                | Some customer ->
                    return [
                        Event.InvoiceCreated {
                            InvoiceId      = InvoiceId cmd.InvoiceId
                            CustomerId     = CustomerId cmd.CustomerId
                            CustomerName   = CustomerName customer.Name
                            CustomerStreet = CustomerStreet customer.Address.Street
                            CustomerCity   = CustomerCity customer.Address.City
                        }
                    ] |> Ok
                
            | Some state, Command.AddInvoiceLine cmd ->
                let! (product:DataTypes.Product option) = dependencies.GetProduct cmd.ProductId
                match product with
                | None ->
                    return Error $"product '{cmd.ProductId}' does not exist"
                | Some product ->
                    return [
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
                return Error "invoice already exists"
            | None, Command.AddInvoiceLine _ ->
                return Error "invoice does not exist"
        }
        
        
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
        

