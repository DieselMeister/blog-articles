namespace Domain

module FsStyleState =

    // the invoice aggregate, here called state
    // also I do not change the equalities to 
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