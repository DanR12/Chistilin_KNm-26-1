# OrderFlow — product description

OrderFlow is a small order-and-invoicing service for an office-supplies wholesaler. Sales managers and customers use it through a web interface; integrators use the same functionality through a REST API. The description below is what the product team wrote for the first release.

## Users and access

There are two kinds of users. A **customer** logs in with a personal account, browses the catalogue, places orders for their own company and can see their own orders and invoices. A **manager** sees all customers and all orders, can place an order on behalf of any customer, and marks paid orders as shipped.

Accounts in the demo installation: `manager` / `manager`; customers `alice` / `alice` (VIP), `bob` / `bob`, `carol` / `carol`. The catalogue can be browsed without logging in.

## Catalogue

The catalogue lists products with a SKU, a name, a price and the quantity in stock. Products can be searched by name or SKU. All prices are quoted including VAT.

## Customers

Each customer has a name, an e-mail address, a postal address and a tier: **Regular** or **VIP**. A customer can edit their own postal address at any time.

## Orders

An order belongs to one customer and consists of lines; each line is a product and a quantity. The subtotal of an order is the sum of its lines.

An order goes through the statuses **Draft → Placed → Paid → Shipped**. Orders created through the web form are placed immediately; the API can create a draft first and add lines to it. An order can be cancelled. When an order is placed, the stock of the ordered products is reduced accordingly and a confirmation e-mail is sent to the customer.

Orders are listed newest first, ten per page, and can be searched by order number or customer name.

## Discounts and shipping

- A discount of **10 %** applies when the subtotal is over 1,000. **VIP customers get 15 %.**
- The promo code **WELCOME** gives a further 5 % on a customer's first order.
- Shipping costs a flat **50**. Shipping is free for orders over 2,000.

## Invoice

An invoice is issued when the order is paid. It shows the seller, the customer's name and billing address, the order lines, the subtotal, the discount, shipping, the net amount, VAT (20 %) added to the net amount, and the total due. The invoice can be viewed in the web interface and retrieved through the API.

## API

The REST API is documented at `/swagger`. It offers the same operations as the web interface: authentication, catalogue search, customers, orders and their lines, order actions, and invoices. Responses are JSON. Errors return an explanatory message with an appropriate status code.

## What is new in release 1.1

Release 1.1 contains no functional changes. It adds the diagnostics that were requested after the first round of testing, and a few more demo orders.

- **Outbox.** Every e-mail the application sends is kept in an outbox, shown on the *Outbox* page and at `GET /api/outbox`. The outbox is emptied when the application restarts.
- **Request log.** Every request is recorded with its time, user, status and duration. A request that fails with an internal error receives an *error id*; the id is shown on the error page and next to the request in the log. The log is on the *Log* page and at `GET /api/log` (manager only).
- **Demo data reset.** `POST /api/test/reset` (manager only), or the *Reset demo data* button on the Log page, deletes all data, restores the demo data and clears the outbox and the log.
- **Support inbox.** Messages that customers and staff send to the product team are forwarded to the *Support* page (manager only).
