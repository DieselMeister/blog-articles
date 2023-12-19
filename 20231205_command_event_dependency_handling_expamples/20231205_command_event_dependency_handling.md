# How to handle external dependencies of your domain in a command-event architecture


## Update: 2023-12-19

I added in the code and here in this article the reader monad. To be complete about this.
Also There is a chapter "The Service" to explain, what is meant with the service implementation.

And I want to add, I mention here sometimes the phrase 'normal dependency injection'.
As you are aware, we use in many cases things like ASP.NET Core and the build in DI container with
the resulting constructor injection, I call 'normal dependency injection'. 
(that seem to bother also some people, that wasn't clear about the term)

I see this all pragmatically and foremost I describe my journey here.

But at least I made a reader monad implementation.

## Introduction

This article will show some examples or should I say more a small journey, how to manage the dependencies in a command-event architecture.
At least for me, these were the steps I took in order to solve the problem for myself.

To be honest all the ways I will describe here, are neither right nor wrong. (okay, the fist one feels totally wrong in F# XD)  
Some of them will feel weird, some of these will break some rules about purity and testability of functions.
These points will be discussed also.

First at all I will try to establish here the example domain I use. And even example domain sound to huge for that.
Please be aware, that this is NOT an article about how to model a proper domain, proper commands and events. 
Please shift your focus not on the domain and how we can improve it. 
In order to avoid the problem I will describe and focus on the dependency management itself and 
how we can deal with external dependencies from a programmer's perspective.

All the code you find here in this github repository: [here](https://github.com/DieselMeister/blog-articles/tree/master/20231205_command_event_dependency_handling_expamples)

## Prerequisite

You should be a little aware about a command-event architecture.   
If you are not, please watch the videos from Greg Young about CQRS and Event Sourcing.   
Also read the book from Scott Wlaschin (Domain Modelling Made Functional).  
Also this one: [here](https://www.youtube.com/watch?v=MHvr71T_LZw&ab_channel=DCF%23Meetup)

## The Example

So let's begin with an example:

Imagine, we have an 'invoice' and when you create this invoice, 
there should be a customer assigned to it. Also, after we created the invoice, 
we want to assign some products or services the customer bought plus the quantity of it.

With this small domain, we want to start. And keep in mind, that we don't want to improve the domain here, 
we want to solve another problem.

## Update: The Service (2023-12-19)
In the code samples you will see a service implementation. 
Consider it as the application layer, application service, domain service or whatever you call it.
It's not important for the discussion here. But it seems, that some people get confused about it.
The service is not the API-Layer like Giraffe HttpHandler or MVC Controller or something like that.
And keep in mind, it's an example and not a real world application.

## The Domain Description

You see here, when we create an invoice, we have to check, if the customer, even exists in our system. 
Also, the nature of an invoice is, that an invoice can not carry a simple reference to our customer data, 
you have to put in the (needed) customer data, name and address, as part of the invoice.
If you change the address of your customer later in your system, 
your invoice is not allowed to change. 
Invoices are immutable (like events in event sourcing) and there are legal things you have to consider. (at least here in Germany, but I think there are similar rules in other countries)

The second thing, we want to do, is assigning/adding some products with some quantity and a price to the invoice. 
Also, here is the same rule, you can not use a reference to your products, 
you have to add at least the name and the price to your invoice.

From a UI perspective, you click on a button 'Create Invoice' 
and the system ask you for which customer you want to create the invoice. 
After that, the system provides some invoice lines, where you select a product or service and enter a quantity.

That's it. And yes, this 'domain' can be solved simple with a CRUD implementation, but as I said, that's not the focus here.

## The Problem

So, what's the problem here? 

You see the invoice uses data, which is, in the most cases stored in your database in particular tables. 
In this case, we have here a table with customer data and a table with products/services your 'company' provides.

And in your domain model, you have to look into the table and check, if the data event exists, 
so it's valid to create an invoice for customer(id), you get from you UI. And if the customer exists, 
we need the data, in order to fill the invoice.

The same is with the products. We can send the name, price and quantity right from the UI, 
but in most cases we trigger the 'addition of a product' by sending a reference-id.

So the problem is, how to deal with the additional data we have, when do we call services or repos, 
in order to check and get the data.


## The (dependent) Data Structures in our System

Before we start, here are some data structures, we use in our examples:
```fsharp
type Product = { ProductId: string; Name: string; Price: decimal }

type Customer = { CustomerId: string; Name: string; Address: Address }
and Address = { Street: string; City: string }
```

We have a product and a customer.
And we have here some Interfaces for the repositories we will use to fetch the actual data.

```fsharp
type IProductRepository =
    abstract member GetProductById : string -> Task<Product option>

type ICustomerRepository =
    abstract member GetCustomerById : string -> Task<Customer option>
```

## Our Domain DataTypes

Our commands, we have 2 of them:
```fsharp
type Command =
    | CreateInvoice of CreateInvoiceData
    | AddInvoiceLine of InvoiceLineData
    
and CreateInvoiceData = {
    InvoiceId: string
    CustomerId: string 
}

and InvoiceLineData = {
    InvoiceId: string
    ProductId: string
    Quantity: int
}
```

The Events:
```fsharp
type Event =
    | InvoiceCreated of InvoiceCreatedData
    | InvoiceLineAdded of InvoiceLineAddedData
    
and InvoiceCreatedData = {
    InvoiceId: InvoiceId
    CustomerId: CustomerId
    CustomerName: CustomerName
    CustomerStreet: CustomerStreet
    CustomerCity: CustomerCity
}

and InvoiceLineAddedData = {
    InvoiceId: InvoiceId
    ProductId: ProductId
    ProductName: ProductName
    ProductPrice: ProductPrice
    ProductQuantity: ProductQuantity
    TotalPrice: TotalPrice
}
```

And the missing value types, you see in the events (they are not important):
```fsharp
type InvoiceId = InvoiceId of string

type CustomerId = CustomerId of string
type CustomerName = CustomerName of string
type CustomerStreet = CustomerStreet of string
type CustomerCity = CustomerCity of string

type ProductId = ProductId of string
type ProductName = ProductName of string
type ProductPrice = ProductPrice of decimal
type ProductQuantity = ProductQuantity of int
type TotalPrice = TotalPrice of decimal
```

## Before We Start
I will provide first the code, and then we talk about it.


## 01. The Classic OOP approach

The classic OOP approach with an aggregate root and an additional aggregate. 
I kept it simple, no internal handling of already replayed and new events, like shown in the Greg Young example.
This approach is not important when your write F#, so you can skip it, if you want.

### The Boilerplate:
```fsharp
type AggregateId = AggregateId of string  

type IAggregate =
    abstract member Id : AggregateId
    
type IAggregateRoot =
    inherit IAggregate
```

### The Domain Code:
```fsharp
type InvoiceAggregateRoot(
    id : AggregateId,
    customerRepo: ICustomerRepository,
    productRepo: IProductRepository) =
    
    // the fields
    let mutable invoiceId = InvoiceId ""
    let mutable customerId = CustomerId ""
    let mutable customerName = CustomerName ""
    let mutable customerStreet = CustomerStreet ""
    let mutable customerCity = CustomerCity ""
    let mutable invoiceLines: InvoiceLineAggregate list = []
    
    let mutable events: Event list = []
    
    interface IAggregateRoot with
        member _.Id = id
    
    // the properties (state)
    member _.InvoiceId        with get() = invoiceId      
    member _.CustomerId       with get() = customerId    
    member _.CustomerName     with get() = customerName  
    member _.CustomerStreet   with get() = customerStreet
    member _.CustomerCity     with get() = customerCity  
    
    member _.InvoiceLines     with get() = invoiceLines 
    
    // the event list
    member _.Events           with get() = events
    
    
    // the methods
    member this.CreateInvoice(Command.CreateInvoice cmd) =
        task {
            let! customer = customerRepo.GetCustomerById cmd.CustomerId
            match customer with
            | None -> failwith "invalid customer id"
            | Some customer ->
                let newEvent =
                    Event.InvoiceCreated {
                        InvoiceId       = InvoiceId cmd.InvoiceId
                        CustomerId      = CustomerId customer.CustomerId
                        CustomerName    = CustomerName customer.Name
                        CustomerStreet  = CustomerStreet customer.Address.Street
                        CustomerCity    = CustomerCity customer.Address.City        
                    }
                events <- newEvent :: events
                this.Apply newEvent
        }
    
    
    member this.AddInvoiceLine (Command.AddInvoiceLine cmd) =
        task {
            let! product = productRepo.GetProductById cmd.ProductId
            match product with
            | None -> failwith "invalid product id"
            | Some product ->
                let newEvent =
                    Event.InvoiceLineAdded {
                        InvoiceId       = InvoiceId cmd.InvoiceId
                        ProductId       = ProductId cmd.ProductId
                        ProductName     = ProductName product.Name
                        ProductPrice    = ProductPrice product.Price
                        ProductQuantity = ProductQuantity cmd.Quantity
                        TotalPrice      = TotalPrice (product.Price * decimal cmd.Quantity)
                    }
                events <- newEvent :: events
                this.Apply newEvent
        }
        
        
    member this.Apply(event) =
        match event with
        | Event.InvoiceCreated ev ->
            invoiceId       <- ev.InvoiceId
            customerId      <- ev.CustomerId
            customerName    <- ev.CustomerName
            customerStreet  <- ev.CustomerStreet
            customerCity    <- ev.CustomerCity
            
        | Event.InvoiceLineAdded ev ->
            let invoiceLine = InvoiceLineAggregate(AggregateId <| Guid.NewGuid().ToString())
            invoiceLine.Apply event
            invoiceLines <- invoiceLine :: invoiceLines
            
            
    member this.ApplyEvents (events: Event list) =
        events |> List.iter (fun event -> this.Apply event)
    
    
and InvoiceLineAggregate(
    id : AggregateId) =
        
    // the fields
    let mutable productId = ProductId ""
    let mutable productName = ProductName ""
    let mutable productPrice = ProductPrice 0.0m
    let mutable productQuantity = ProductQuantity 0
    let mutable totalPrice = TotalPrice 0.0m
    
    interface IAggregate with
        member _.Id = id        
        
    // the properties
    member _.ProductId          with get() = productId       
    member _.ProductName        with get() = productName     
    member _.ProductPrice       with get() = productPrice    
    member _.ProductQuantity    with get() = productQuantity 
    member _.TotalPrice         with get() = totalPrice      
    
    // the methods
    
    member _.Apply (event:Event) =
        match event with
        | Event.InvoiceLineAdded event ->
            productId       <- event.ProductId
            productName     <- event.ProductName
            productPrice    <- event.ProductPrice
            productQuantity <- event.ProductQuantity
            totalPrice      <- event.TotalPrice
        | _ ->
            failwith "invalid event"

```

### The Service Code:
```fsharp
    type IInvoiceRepository =
        abstract member GetInvoice : invoiceId:string -> Task<InvoiceAggregateRoot option>
        abstract member StoreInvoice : InvoiceAggregateRoot -> Task<unit>
    
    
    type IInvoiceService =
        abstract member CreateInvoice : invoiceId:string -> customerId:string -> Task<unit>
        abstract member AddInvoiceLine : invoiceId:string -> productId:string -> quantity:int -> Task<unit>
        
     
    type IInvoiceServiceAlternative =
        abstract member ExecuteInvoiceCommand : command:Command -> Task<unit>
    
    
    type InvoiceService(
        invoiceRepo: IInvoiceRepository,
        customerRepo: ICustomerRepository,
        productRepo: IProductRepository
        ) =
        interface IInvoiceService with
        
            member this.CreateInvoice invoiceId customerId =
                task {
                    let! invoice = invoiceRepo.GetInvoice invoiceId
                    let aggregateId = AggregateId (invoiceId)
                    let invoiceAggregate = InvoiceAggregateRoot(aggregateId, customerRepo, productRepo)
                    let command = Command.CreateInvoice { InvoiceId = invoiceId; CustomerId = customerId }
                    do! invoiceAggregate.CreateInvoice command
                    do! invoiceRepo.StoreInvoice invoiceAggregate
                }
                
                
            member this.AddInvoiceLine invoiceId productId quantity =
                task {
                    let! invoiceAggregate = invoiceRepo.GetInvoice invoiceId
                    match invoiceAggregate with
                    | None -> failwith "Invoice not found"
                    | Some invoiceAggregate ->
                        let command = Command.AddInvoiceLine { InvoiceId = invoiceId; ProductId = productId; Quantity = quantity }
                        do! invoiceAggregate.AddInvoiceLine command
                        do! invoiceRepo.StoreInvoice invoiceAggregate
                }
                
                
    type InvoiceServiceAlternative(
        invoiceRepo: IInvoiceRepository,
        customerRepo: ICustomerRepository,
        productRepo: IProductRepository
        ) =
        interface IInvoiceServiceAlternative with
        
            member this.ExecuteInvoiceCommand command =
                task {
                    let! invoice = invoiceRepo.GetInvoice command.InvoiceId
                    match command with
                    | Command.CreateInvoice cmd ->
                        match invoice with
                        | None ->
                            let aggregateId = AggregateId (cmd.InvoiceId)
                            let invoiceAggregate = InvoiceAggregateRoot(aggregateId, customerRepo, productRepo)
                            do! invoiceAggregate.CreateInvoice command
                            do! invoiceRepo.StoreInvoice invoiceAggregate
                        | Some _ ->
                            failwith "Invoice already exists"
                            
                    | Command.AddInvoiceLine _ ->
                        match invoice with
                        | None -> failwith "Invoice not found"
                        | Some invoiceAggregate ->
                            do! invoiceAggregate.AddInvoiceLine command
                            do! invoiceRepo.StoreInvoice invoiceAggregate
                        
                }
```


### The Discussion:

Please be aware, that this is one way to implement a command-event driven domain in oop. 
There are probably multiple ways. 
It's a little based on Greg Young's 'simples-cqrs example' ([here](https://github.com/gregoryyoung/m-r)).

So let's discuss this approach from the perspective of handling the dependencies and the 
additional data we need from the database in order to fill the events and out final state:

The 'classic OOP' has the 'advantage' that, it's pretty forward in handling the dependencies. 
It uses classic dependency injection and injects the necessary repository interfaces into the constructor of the aggregate root itself.
The methods 'CreateInvoice' and 'AddInvoiceLine' in the domain code call the repositories to get the data and 
if they don't get them, they throw an exception. 
(I know and hope, this time around, the people will also work with results in C#, this example above is not idiomatic F# at all)

That's a pretty forward implementation. It's easy to achieve, beside the huge boilerplate code, 
but if you implement that in C# it's a common approach. 
(We will in the next approach, how we 'optimize' that approach and make it more 'functional')

#### What about testing?

You have to Mock all interfaces, you use. 
That is in OOP a total normal approach. 
But it's tedious to mock. 
Also, it's also not a pure implementation of the domain, which can cause issues down the line.

#### Conclusion

It's not something I would ever implement in F#. 
Also not in C# anymore, because I know better now. 
I didn't like the whole encapsulation stuff and these Aggregate-Root and sub Aggregate stuff. So for me personal a no.
In my opinion the whole stuff came up, when someone in the OOP world said, that 'anemic domain model' are an anti-pattern.
Nevertheless, it's a valid approach for people who like it. 
It's not wrong. I harder to test, maybe harder to understand and the injection of the interfaces can cause issues, 
but it's easy and straight forward.


## 02. The More F# Idiomatic Way

So, let's see, how we can do that in a more F# idiomatic way.

### The State:
```fsharp
type InvoiceState = {
    InvoiceId      : InvoiceId 
    CustomerId     : CustomerId
    CustomerName   : CustomerName
    CustomerStreet : CustomerStreet
    CustomerCity   : CustomerCity
    
    InvoiceLines   : InvoiceLine list
}
and InvoiceLine = {
    ProductId        : ProductId
    ProductName      : ProductName
    ProductPrice     : ProductPrice
    ProductQuantity  : ProductQuantity
    TotalPrice       : TotalPrice
}
```

### The Domain Code:
```fsharp
// execute a command (decider)
let execute getCustomer getProduct command state =
    task {
        match state, command with
        | None, Command.CreateInvoice cmd ->
            let! (customer:DataTypes.Customer option) = getCustomer cmd.CustomerId
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
            let! (product:DataTypes.Product option) = getProduct cmd.ProductId
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
```


### The Service Code:
```fsharp
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
```

### The Discussion:

The first thing you see is, that we are not using any aggregate root or sub aggregates.
We are using a simple state, which is a record type.
You can argue about that and the whole aggregate id and not structural equality stuff, but that's not the focus here.

The domain code is pretty simple.
You have an 'execute' function, which decides, if a command is valid or not. It returns a list of events or an error.
We have an 'apply' function, which applies an event to a state and returns a new state.
And we have an 'applyEvents' function, which applies a list of events to a state and returns a new state.
In our further discussion, the 'apply' and 'applyEvents' function will not change any more, so I will leave them out of the code.

Let's focus on the 'execute' function. In order to generate the necessary events, we need, like in the OOP approach,
additional data from the database. Our customer and out product.
In this approach we inject the necessary functions to get the data from the database into the 'execute' function.

Later in the service code, we partially apply these functions with the help of your repository interfaces.

Did this approach help us with the dependency management? No. If you think about this approach, this approach tends to
get fast out of hands by using more and more parameters in the 'execute' function.
In the OOP approach these parameters inside the constructor feels more natural and 
the execution methods do not have that many parameters.


#### What about testing?

So instead of mocking interfaces, we have to mock functions and injecting them into the 'execute' function. That doesn't feel better at all.

#### Conclusion

The idiomatic F# approach feels more comfortable and in it's core more elegant, then the OOP approach. 
We using the functional way to separate data and functions. But we get this parameter problem, which gets out of hands pretty fast.
Also the execute function is not pure. We are using a task an call the repositories inside the execute function.

How we handle the problem with parameters, we will see in the next chapter. For the other problem, we have to wait.


## 03. Try Hiding The Dependencies (Mark I)

So, let's see, how we can hide the dependencies in the execute function in order to get rid of the parameter problem. At least partially.

### The Domain Code:
```fsharp
type Dependencies = {
        GetCustomer : string -> Task<DataTypes.Customer option>
        GetProduct  : string -> Task<DataTypes.Product option>
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
```

### The Service Code:
```fsharp
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
    
    // ...
    
```

### The Discussion:

What we here did is to collect all the parameters to one record we call 'Dependencies'. Yes, I know, that sounds creative, I know.
Also there is nothing much to discuss. At least we reduced the dependencies to one parameter.
The Dependency record will grow fast and it has to be initialized fully before we can use it.

#### What about testing?

Nothing changed in this approach.

#### Conclusion

We didn't solve the problem, we just moved it to another place. But at least our code doesn't look too bad on the domain level. Now it's elsewhere in our code.


## 04. More Functional Purity and Testability

Okay, now that our execute function only takes 3 parameters and we can focus on testability.
A wonderful approach to get better testability is to make our domain a pure function. 
With that we get always the same result for the same input.

### The Domain Code:
```fsharp
type Dependencies = {
        NeededCustomerForCreateInvoice : DataTypes.Customer option
        NeededProductForAddInvoiceLine : DataTypes.Product option
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
```

### The Service Code:
```fsharp
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
```


### The Discussion:

In order to get our 'execute' function pure, we removed all the external calls from the code and the 'dependencies' record.
It's also not a task computation anymore.
What we have here now, is the pure data inside the dependency record we need to execute the command.
With that the whole 'execute' function become pure and better testable.
We build our repositories, that they return option types, so we can easily left the check, if a customer or product exists,
like it already was. If it's none, then it's not there. Easy enough. 

With that, we moved the external calls to the database up to the service implementation.
It's now a 'impure-pure-impure' sandwich on the service level.

You see there, that the dependencies are build based on the command, which came in. This is an impure part.
After that, we call the 'pure' execute function and get the result and after that,
we call the database to store our new state. This one is again 'impure'.

What's with the apply function? That's a good question. Because we throw in our example exceptions there,
the apply function is considered impure. I leave you to decide, which approach you want. The function can easily be made pure.
But keep in mind, that a missing application of an event to a state is, for me at least, considered an exceptional error in your system.
It shouldn't happen, if it is happening, your code is wrong and you have to fix it.
My personal opinion about that is to throw an exception.

Btw. the code of the 'apply' you see in the chapter '02. The More F# Idiomatic Way'. I left it out, because it's the same in part 2 to 5.

One thing, the dependency record can grow bigger and bigger and you will have problem, which property is for what command.
And that can became a problem. Use proper names in order to know which one blogs to which one.

#### What about testing?

Now that we have a pure function, we are basically in the testing heaven. Same input generates the same output. What do we want more?
We have to write test data nevertheless or use property based testing to generate the data for us. (I never tried property testing at all.)

#### Conclusion

We are almost at the end of our journey. We have now a pure function and only one additional parameter in our execute function.

But for me, we only moved the our problem one level up. And I am not satisfied with that. Because our dependency record can grow and grow
and for every additional data we need, wee need to add a property there. And that maybe become confusing over time.
Even if you use proper names for the properties. It feels like, that the dependency record should not be responsible for all the needed dependencies of you domain.

For small domains, with not that much additional data, it's totally fine in my option. But can we do better?


## 05. Try Hiding The Dependencies (Mark II)

So that's the last approach we discuss here for now. Here we want to solve the problem, 
that we have basically one dependency record for all the commands in our domain. 
And somehow every command should manage there own needed dependencies.

And that's what we will do here and not break the purity of our execute function.

### The Domain Code:
```fsharp
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
```

### The Service Code:
```fsharp
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
```

### The Discussion:

What we here did is introduce the concept on an internal and external command.
The external commands, are the commands, you use in your api.
The internal commands have additional properties in their payload, which are relevant for the execution of the command.

That sound tedious, because now you have to maintain two command types. But the advantage you get, is that you have
the dependencies inside the command itself, so you didn't have to maintain the dependency record from the previous chapter.

I mentioned in the previous chapter that the dependency record approach is fine when you have a small amount of additional data.

But if when you get more, it makes sense to move the dependencies to the command itself and have clear 'responsibilities'.

And let's be honest, copy-pasting the commands you already have and extend them is not that much work.

Also some of you maybe already have dto's in you api, and do not use the commands we have here at all.
In that case, you have only one set of commands with the necessary dependencies and do the 'mapping' already on the service level.


#### What about testing?

We are already in testability heaven and that didn't change here.

#### Conclusion

As I mentioned, that's the approach I personally prefer.
It's a little more work (maybe), but it's more clear and you have a better separation of concerns.

And least for me, I am satisfied with this solution and it solves perfectly my problem to manage the dependencies and additional data I need in my domains.


## The Previous Version: The Reader Monad

Yes, I know, I know. I didn't mention the reader monad at all. You see in the examples, that I use on service level
the classic oop dependency injection approach, because in practical terms I will use for these services asp.net core and for example giraffe or Azure Functions.
And the build in dependency injection container works fine and we didn't have to make it more complicated.

Also I didn't work out any solution with a reader monad. I invite you, I am curios, how you would exactly this example with a reader monad.
And which advantage you can in readability and testability.

I personally do not like to much 'magic*' in the code, where things are suddenly hidden. Someone has to maintain it. 
But that's my personal opinion.

*I mean writing code with F# feels like magic, if you compare it to C# or Java.

BUT I really like to see a solution with a reader monad. So if you have one, please let me know.
(I found someone :D)


## Updated: 06. The Reader Monad

With the help of two articles, one from Bartosz Sypytkowski ([here](https://www.bartoszsypytkowski.com/dealing-with-complex-dependency-injection-in-f/)) and one from Scott Wlaschin ([here](https://fsharpforfunandprofit.com/posts/dependencies-3/))
I made an implementation of a reader monad on the service level. 
The domain code itself is pure and the reader monad didn't change there anything.

### The Reader Monad Implementation (called here Dependency - call it whatever you want)
```fsharp
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
```

### A New Interface
```fsharp
type IDependencies =
    inherit IInvoiceRepository
    inherit ICustomerRepository
    inherit IProductRepository
```


### The Service Code:
```fsharp
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
    
    
    
type InvoiceService(dependencies:IDependencies) =
    interface IInvoiceService with
        member this.ExecuteInvoiceCommand command =
            Dependency.run dependencies (executeCommand command)
```

### The Discussion:

Okay, I read some blog posts and tried to understand the reader monad. 
(it seems to offend people, not to use one!)

So the implementation of the reader monad you see everywhere an I will not describe them.
But I encourage you to read the blog posts I mentioned above.

In the end you have a computation expression (here called 'dependency'), 
which you can use to 'inject' your dependencies into your code.

A base thing here is that we unify all our dependencies into one interface. 
And then hide the 'injection' with the help of the reader monad.

The cool thing here is, that I can call functions inside the reader monad, 
which are also reader monads and so one, but for this simple example,
we only hide this one dependency interface.

I definitely have to got deeper in it and see the advantage of it.

#### What about testing?

same as before. We have a pure function and we can test it easily.

#### Conclusion

The reader monad is a cool thing. I changes how I inject the things.

And now came the BUT for this example and an implementation for a 'command executor'.

I can also inject this single interface as an additional parameter.
```fsharp
let private executeCommand
    (env:IDependencies)
    (command:ExternalCommand) =
    //...
```
(In the code you see it as the file 07)

Here you see, I did it! I injected the dependencies as an additional parameter.

Because I have here only this one function, which is not a composition of multiple functions with multiple dependencies.
And for this particular example, I don't see the advantage of the reader monad.

And again BUT: If the application grows and you have more and more dependencies, 
then the reader monad is a cool thing to hide the dependencies and make the code more readable.
In the end you need only once some 'boilerplate' to have the code for the monad and then you can use it everywhere.

Also a reader monad makes you code not magically pure. If you inject async operations, then you are not pure at all.



## Last word (your honor) :D

I hope you liked this little journey through the different approaches to handle dependencies in your domain.
For me I have found my personal favorite approach to handle additional data in my domain.

I am curios, what did you do in order to handle this problem. Please let me know.

### Merry Christmas and a happy new year to all of you. You are an awesome community!
  
  
  
Twitter: @HardtITSolution  
The Code: [here](https://github.com/DieselMeister/blog-articles/tree/master/20231205_command_event_dependency_handling_expamples)



