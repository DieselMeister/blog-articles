# [Here a title]

### Introduction

This article will show some examples and ways to manage the dependencies in a command-event architecture. What are the pros and what are the cons.

To be honest all the ways I will describe here, are neither right nor wrong. Some of them will feel weird, some of these will break some rules about purity of functions.

First at all I will try to establish here the example domain I use. And even example domain sound to huge for that.
Please be aware, that this is NOT an article about how to model a proper domain, proper commands and events. Please shift your focus not on the domain and how we can improve it. 
In order to avoid the problem I will describe and focus on the dependency management itself and how we can deal with external dependencies from a programmer's perspective.

### Prerequisite

You should be a little aware about a command-event architecture. If you are not, please watch the videos from Greg Young, also read the book from Scott Wlaschin.


### The Example

So let's begin with an example:

Imagine, we have an 'invoice' and when you create this invoice, there should be a customer assigned to it. Also, after we created the invoice, we want to assign some products or services the customer bought plus the quantity of it.

With this small domain, we want to start. And keep in mind, that we don't want to improve the domain here, we want to solve another problem.

### The Domain Description

You see here, when we create an invoice, we have to check, if the customer, even exists in our system. Also, the nature of an invoice is, that an invoice can not carry a simple reference to our customer data, you have to put in the (needed) customer data, name and address, as part of the invoice.
If you change the address of your customer later in your system, your invoice is not allowed to change. Invoices are immutable (like events in event sourcing) and there are legal things you have to consider. (at least here in Germany, but I think there are similar rules in other countries)

The second thing, we want to do, is assigning/adding some products with some quantity and a price to the invoice. Also, here is the same rule, you can not use a reference to your products, you have to add at least the name and the price to your invoice.

From a UI perspective, you click on a button 'Create Invoice' and the system ask you for which customer you want to create the invoice. After that, the system provides some invoice lines, where you select a product or service and enter a quantity.

That's it. And yes, this 'domain' can be solved simple with a CRUD implementation, but as I said, that's not the focus here.

### The Problem

So, what's the problem here? 

You see the invoice uses data, which is, in the most cases stored in your database in particular tables. In this case, we have here a table with customer data and a table with products/services your 'company' provides.

And in your domain model, you have to look into the table and check, if the data event exists, so it's valid to create an invoice for customer(id), you get from you UI. And if the customer exists, we need the data, in order to fill the invoice.

The same is with the products. We can send the name, price and quantity right from the UI, but in most cases we trigger the 'addition of a product' by sending a reference-id.

So the problem is, how to deal with the additional data we have, when do we call services or repos, in order to check and get the data.


### The (dependent) Data Structures in our System

Before we start, here are some data structures, we use in our examples:
```fsharp
type Product = { ProductId: string; Name: string; Price: decimal }

type Customer = { CustomerId: string; Name: string; Address: Address }
and Address = { Street: string; City: string; State: string; Zip: string }
```

We have a product and a customer.
And we have here some Interfaces for the repositories we will use to fetch the actual data.

```fsharp
type IProductRepository =
        abstract member GetProductById : string -> Task<Product option>

type ICustomerRepository =
    abstract member GetCustomerById : string -> Task<Customer option>
```

### Our Domain DataTypes

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

### Before We Start
I will provide first the code, and then we talk about it.


### The Classic OOP approach

The classic OOP approach with an aggregate root and an additional aggregate. I kept it simple, so no interface for the aggregate root, to keep it easy to follow:


