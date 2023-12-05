namespace Dependencies

module DataTypes =

    type Product = { ProductId: string; Name: string; Price: decimal }

    type Customer = { CustomerId: string; Name: string; Address: Address }
    and Address = { Street: string; City: string }
    
module Repositories =
    
    open System.Threading.Tasks
    open DataTypes

    type IProductRepository =
        abstract member GetProductById : string -> Task<Product option>

    type ICustomerRepository =
        abstract member GetCustomerById : string -> Task<Customer option>

    type ProductRepository() =
        interface IProductRepository with
            member this.GetProductById id =
                task {
                    match id with
                    | "1" -> return Some { ProductId = "1"; Name = "Product 1"; Price = 1.00m }
                    | "2" -> return Some { ProductId = "2"; Name = "Product 2"; Price = 2.00m }
                    | _ -> return None    
                }

    type CustomerRepository() =
        interface ICustomerRepository with
            member this.GetCustomerById id =
                task {
                    match id with
                    | "1" -> return Some { CustomerId = "1"; Name = "Customer 1"; Address = { Street = "123 Main St"; City = "Anytown"; State = "ST"; Zip = "12345" } }
                    | "2" -> return Some { CustomerId = "2"; Name = "Customer 2"; Address = { Street = "456 Main St"; City = "Anytown"; State = "ST"; Zip = "12345" } }
                    | _ -> return None    
                }
