namespace Domain



module TryHidingMark2 =


    open System.Threading.Tasks
    open Dependencies
    open FsStyleState
    open Dependencies.DataTypes
    
    // we putting our dependencies into our command, for that we add some "internal" commands
    // we distinguish between commands which came through our api and command which we are using internally
    
    // our external commands
    [<RequireQualifiedAccess>]
    type ExternalCommand =
        | CreateInvoice of ExternalCreateInvoiceData
        | AddInvoiceLine of ExternalInvoiceLineData
            
            member this.InvoiceId =
                match this with
                | CreateInvoice data -> data.InvoiceId
                | AddInvoiceLine data -> data.InvoiceId
        
    and ExternalCreateInvoiceData = {
        InvoiceId: string
        CustomerId: string 
    }

    and ExternalInvoiceLineData = {
        InvoiceId: string
        ProductId: string
        Quantity: int
    }
    
    
    // our internal commands
    [<RequireQualifiedAccess>]
    type Command =
        | CreateInvoice of CreateInvoiceData
        | AddInvoiceLine of InvoiceLineData
            
            member this.InvoiceId =
                match this with
                | CreateInvoice data -> data.InvoiceId
                | AddInvoiceLine data -> data.InvoiceId
        
    and CreateInvoiceData = {
        InvoiceId: string
        CustomerId: string
        Customer: Customer option
    }

    and InvoiceLineData = {
        InvoiceId: string
        ProductId: string
        Quantity: int
        Product: Product option
    }
    
    

    // execute a command (decider)
    let execute command (state:InvoiceState option) =
        match state, command with
        | None, Command.CreateInvoice cmd ->
            match cmd.Customer with
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
            match cmd.Product with
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
            
        | Some state, Command.CreateInvoice cmd ->
            Error $"invoice '{cmd.InvoiceId}' already exists"
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