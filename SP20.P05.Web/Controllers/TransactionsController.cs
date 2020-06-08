﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SP20.P05.Web.Data;
using SP20.P05.Web.Features.Authentication;
using SP20.P05.Web.Features.Cart_and_transactions;
using SP20.P05.Web.Features.FarmFields;
using SP20.P05.Web.Features.FarmFieldTickets;

namespace SP20.P05.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly UserManager<User> userManager;

        public TransactionsController(DataContext context, UserManager<User> userManager)
        {
            this._context = context;
            this.userManager = userManager;
        }

        private List<ItemDto> MapItemstoItemsDTO(List<Item> myItems)
        {
            List<ItemDto> listDto = new List<ItemDto>();

            foreach (var item in myItems)
            {
                var ffTicketId = _context.Items.Where(x => x.farmFieldTicketId == item.farmFieldTicketId).Select(y => y.farmFieldTicketId).FirstOrDefault();
                var ffId = _context.FarmFieldTickets.Where(y => y.Id == ffTicketId).Select(y => y.FarmFieldId).FirstOrDefault();
                var ffname = _context.FarmFields.Where(x => x.Id == ffId).Select(y => y.Name).FirstOrDefault();
                var price = item.Price;
                var sizeName = _context.Size.Where(x => x.Id == item.SizeId).Select(y => y.BucketSizeNames).FirstOrDefault();

                var newItemDto = new ItemDto() { FarmFieldId = ffId, SizeId = item.SizeId, FarmFieldName = ffname, SizeName = sizeName, Quantity = item.Quantity, Price = price };
                listDto.Add(newItemDto);
            }
            return listDto;
        }

        [HttpGet]
        public List<TransactionDto> GetTransaction(int? CartId)
        {

            Nullable<int> cartId = null;

            if (CartId == null)
            {
                var currentlyLoggedInCustomer = Int32.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                cartId = _context.Set<Cart>().Where(x => x.UserId == currentlyLoggedInCustomer).Select(y => y.Id).FirstOrDefault();
            }
            else
            {
                cartId = CartId;
            }

            //if (cartId == null || cartId <= 0)         
            //{
            //    return new List<TransactionDto>();
            //}

            //finding transactions of a particular user
            var transactions = _context.Set<Transaction>().Include(x => x.Items).Where(x => x.cartId == cartId).ToList();

            //creating transactiondto to send to front end
            var transactionsDto = new List<TransactionDto>();

            foreach (var trans in transactions)
            {
                //mapping trx props

                List<ItemDto> myItemDto = MapItemstoItemsDTO(trans.Items);

                var newDto = new TransactionDto()
                {
                    Id = trans.Id,
                    TotalPrice = trans.TotalPrice,
                    cartId = trans.cartId,
                    Date = trans.Date,
                    ItemsDto = myItemDto,
                    Redeemed = trans.Redeemed
                };
                //mapping each item in trx to itemDto
                transactionsDto.Add(newDto);
            }

            return transactionsDto;
        }

        [HttpGet("all")]          //to be called by admin to display all the transactions
        public List<CartDto> GetAllCarts()
        {
            var listOfCarts = _context.Cart.ToList();
            List<CartDto> listOfCartDtos = new List<CartDto>();
            foreach (var cart in listOfCarts)
            {
                var cartDtoId = cart.Id;
                var userName = _context.Set<User>().Where(x => x.Id == cart.UserId).Select(y => y.UserName).FirstOrDefault();
                List<TransactionDto> listOfTransactionsForACart = GetTransaction(cart.Id);

                var cartDto = new CartDto()
                {
                    Id = cartDtoId,
                    UserName = userName,
                    TransactionDtos = listOfTransactionsForACart
                };
                listOfCartDtos.Add(cartDto);
            }

            return listOfCartDtos;
        }

        // public List<TransactionDto> MapTransactionToDTO(List<Transaction>)

        // POST: api/Transactions
        //To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]       //note::::::::!!!! farmfieldid and sizename needed in itemdto to create transaction
        public async Task<ActionResult<Transaction>> CreateTransaction(List<ItemDto> itemDto)
        {

            var currentlyLoggedInCustomer = Int32.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            Nullable<int> cartId = _context.Set<Cart>().Where(x => x.UserId == currentlyLoggedInCustomer).Select(y => y.Id).FirstOrDefault();

            if (cartId == null || itemDto[0].FarmFieldId == 0)
            {
                return BadRequest();
            }



            // creating farm field tickets first for items table from the farmfieldId
            var listOfTicketIds = new List<int>();
            for (int i = 0; i < itemDto.Count; i++)
            {
                var farmfieldticketdto = new FarmFieldTicketDto();
                farmfieldticketdto.FarmFieldId = itemDto[i].FarmFieldId;
                var farmFieldTicketId = CreateFarmFieldTickets(farmfieldticketdto); //sending dto to another method to create tickets
                if (farmFieldTicketId == 0)
                    return BadRequest();
                else
                    listOfTicketIds.Add(farmFieldTicketId);
            }

            //creating list of items for transaction table
            var listOfItems = new List<Item>();
            var totalPrice = 0;     //total price for transaction table

            for (int i = 0; i < listOfTicketIds.Count; i++)
            {
                var unitPrice = itemDto[i].Price;
                var tempSizeId = _context.UnitPrices.Where(x => x.farmFieldId == itemDto[i].FarmFieldId && x.rate == unitPrice).Select(y => y.sizeId).FirstOrDefault();
                //var unitPrice = _context.UnitPrices.Where(x => x.farmFieldId == itemDto[i].FarmFieldId && x.sizeId == itemDto[i].SizeId).Select(y => y.rate).FirstOrDefault();

                var item = new Item
                {
                    farmFieldTicketId = listOfTicketIds[i],
                    SizeId = tempSizeId,
                    Quantity = itemDto[i].Quantity,
                    Price = unitPrice * (itemDto[i].Quantity)

                };
                totalPrice += item.Price;
                listOfItems.Add(item);
                _context.Items.Add(item);
            }


            var newTransaction = new Transaction()
            {
                Items = listOfItems,
                TotalPrice = totalPrice,
                cartId = (int)cartId,
                Redeemed = false,
                Date = System.DateTime.Now
            };
            _context.Transactions.Add(newTransaction);
            await _context.SaveChangesAsync();

            return Created(string.Empty, newTransaction.Id);

        }

        private int CreateFarmFieldTickets(FarmFieldTicketDto farmfieldticketdto)
        {
            if (_context.FarmFields.Any(x => x.Active && x.Id == farmfieldticketdto.FarmFieldId))
            {

                var addedItem = _context.FarmFieldTickets.Add(new FarmFieldTicket
                {
                    FarmFieldId = farmfieldticketdto.FarmFieldId

                });

                _context.SaveChanges();

                return addedItem.Entity.Id;
            }
            else
                return 0;


        }

        [HttpPost("redeem/{cartId}/{transactionId}")]
        public ActionResult RedeemTransaction(int cartId, int transactionId)
        {
            var transaction = _context.Set<Transaction>().Where(x => x.cartId == cartId && x.Id == transactionId).FirstOrDefault();
            if (transaction.Redeemed == true) {
                return BadRequest();
            }
            transaction.Redeemed = true;
            _context.SaveChanges();
            return Ok();
        }
    }

}






//  return CreatedAtAction("GetTransaction", new { id = transaction.Id }, transaction);




// GET: api/Transactions
//[HttpGet]
//public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions()
//{
//    return await _context.Transactions.ToListAsync();
//}

//// GET: api/Transactions/5
//[HttpGet("{id}")]
//public async Task<ActionResult<Transaction>> GetTransaction(int id)
//{
//    var transaction = await _context.Transactions.FindAsync(id);

//    if (transaction == null)
//    {
//        return NotFound();
//    }

//    return transaction;
//}



// PUT: api/Transactions/5
// To protect from overposting attacks, please enable the specific properties you want to bind to, for
// more details see https://aka.ms/RazorPagesCRUD.
//[HttpPut("{id}")]
//public async Task<IActionResult> PutTransaction(int id, Transaction transaction)
//{
//    if (id != transaction.Id)
//    {
//        return BadRequest();
//    }

//    _context.Entry(transaction).State = EntityState.Modified;

//    try
//    {
//        await _context.SaveChangesAsync();
//    }
//    catch (DbUpdateConcurrencyException)
//    {
//        if (!TransactionExists(id))
//        {
//            return NotFound();
//        }
//        else
//        {
//            throw;
//        }
//    }

//    return NoContent();
//}


//// DELETE: api/Transactions/5
//[HttpDelete("{id}")]
//public async Task<ActionResult<Transaction>> DeleteTransaction(int id)
//{
//    var transaction = await _context.Transactions.FindAsync(id);
//    if (transaction == null)
//    {
//        return NotFound();
//    }

//    _context.Transactions.Remove(transaction);
//    await _context.SaveChangesAsync();

//    return transaction;
//}

//private bool TransactionExists(int id)
//{
//    return _context.Transactions.Any(e => e.Id == id);
//}


