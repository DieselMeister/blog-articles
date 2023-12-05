module Domain

[<RequireQualifiedAccess>]
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



[<RequireQualifiedAccess>]
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


