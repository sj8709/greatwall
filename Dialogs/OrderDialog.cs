using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Threading.Tasks;           //Add to process Async Task
using Microsoft.Bot.Connector;          //Add for Activity Class
using Microsoft.Bot.Builder.Dialogs;    //Add for Dialog Class
using System.Net.Http;                  //Add for internet
using GreatWall.Helpers;                //Add for CardHelper

using System.Data;                      //Add for DB Connection
using System.Data.SqlClient;            //Add for DB Connection
using GreatWall.Model;                  //Add for Model

namespace GreatWall
{
    [Serializable]
    public class OrderDialog : IDialog<string>
    {
        private string strMessage = null;

        private string strServerUrl = "http://localhost:3984/Images/";

        private string strSQL = "SELECT * FROM Menus";

        List<OrderItem> MenuItems = new List<OrderItem>();   //Create list object

        public async Task StartAsync(IDialogContext context)   
        {
            //Calling MessageReceivedAsync() without waiting for user input message 
            await this.MessageReceivedAsync(context, null);  
        }

        private async Task MessageReceivedAsync(IDialogContext context,
                                               IAwaitable<object> result)
        {
            if (result != null)
            {
                Activity activity = await result as Activity;

                if (activity.Text.Trim() == "Exit")
                {
                    List<ReceiptItem> receiptItems = new List<ReceiptItem>();
                    Decimal totalPrice = 0;

                    foreach (OrderItem orderItem in MenuItems)
                    {
                        receiptItems.Add(new ReceiptItem()
                        {
                            Title = orderItem.Title,
                            Price = orderItem.Price.ToString("##########"),
                            Quantity = orderItem.Quantity.ToString(),
                        });

                        totalPrice += orderItem.Price;
                    }

                    
                    //Setting query parameter
                    SqlParameter[] para =
                    {
                        new SqlParameter("@TotalPrice", SqlDbType.SmallMoney),
                        new SqlParameter("@UserID", SqlDbType.NVarChar, 50)
                    };

                    para[0].Value = totalPrice;
                    para[1].Value = activity.Id;

                    //Store ordered menu -> Orders table
                    SQLHelper.ExecuteNonQuery("INSERT INTO Orders(TotalPrice, UserID, OrderDate) " +
                                              "VALUES(@TotalPrice, @UserID, GETDATE())", para);

                    DataSet orderNumber = SQLHelper.RunSQL("SELECT MAX(OrderID) FROM Orders " +
                                                           "WHERE UserID = '" + activity.Id + "'");

                    DataRow row = orderNumber.Tables[0].Rows[0];
                    int orderID = (int)row[0];

                    foreach (OrderItem orderItem in MenuItems)
                    {
                        //Setting query parameter 
                        SqlParameter[] para2 =
                        {
                            new SqlParameter("@OrderID", SqlDbType.Int),
                            new SqlParameter("@ItemName", SqlDbType.NVarChar),
                            new SqlParameter("@ItemPrice", SqlDbType.SmallMoney),
                            new SqlParameter("@Quantity", SqlDbType.Int)
                        };

                        para2[0].Value = orderID;
                        para2[1].Value = orderItem.Title;
                        para2[2].Value = orderItem.Price;
                        para2[3].Value = orderItem.Quantity;

                        //Store ordered menu -> Items table
                        SQLHelper.ExecuteNonQuery(
                                        "INSERT INTO Items(OrderID, ItemName, ItemPrice, Quantity) " +
                                        "VALUES(@OrderID, @ItemName, @ItemPrice, @Quantity)", para2);

                    }

                    //Ordered menu output 
                    var cardMessage = context.MakeMessage();
                    cardMessage.Attachments.Add(
                        CardHelper.GetReceiptCard("[Ordered Menu List] \n", receiptItems, 
                                                   totalPrice.ToString(), "2%", "10%"));

                    MenuItems.Clear();

                    await context.PostAsync(cardMessage);
                    context.Done("Order Completed");
                }
                else
                {
                    //DB Connection using SQLHelper
                    string strSQL = "SELECT * FROM Menus WHERE MenuID = " + activity.Text;
                    DataSet DB_DS = SQLHelper.RunSQL(strSQL);
                    DataRow row = DB_DS.Tables[0].Rows[0];
                    
                    //Select data -> Insert List
                    MenuItems.Add(new OrderItem
                    {
                        ItemID = (int)row["MenuID"],
                        Title = row["Title"].ToString(),
                        Price = (Decimal)row["Price"],
                        Quantity = 1
                    });

                    //Show ordered menu 
                    string strOrderMenus = "You ordered...\n ";
                    foreach (OrderItem orderItem in MenuItems)
                    {
                        strOrderMenus += orderItem.Title + ": " + 
                                         orderItem.Price.ToString("########") + "\n\n";
                    }

                    await context.PostAsync(strOrderMenus);

                    context.Wait(this.MessageReceivedAsync);
                }
            }
            else
            {
                strMessage = "[Food Order Menu] Select the menu you want to order.> ";
                await context.PostAsync(strMessage);    //return our reply to the user

                //DB Connection using SQLHelper
                DataSet DB_DS = SQLHelper.RunSQL(strSQL);
                
                //Menu
                var message = context.MakeMessage();
                foreach (DataRow row in DB_DS.Tables[0].Rows)
                {
                    //Hero Card-01~04 attachment 
                    message.Attachments.Add(CardHelper.GetHeroCard(row["Title"].ToString(), 
                                            row["Price"].ToString(), 
                                            this.strServerUrl + row["Images"].ToString(), 
                                            row["Title"].ToString(), row["MenuID"].ToString()));
                }           
                
                message.Attachments.Add(CardHelper.GetHeroCard("Exit food order...", "Exit",
                                        null, "Exit Order", "Exit"));

                message.AttachmentLayout = "carousel";              //Setting Menu Layout Format
               
                await context.PostAsync(message);                   //Output message 

                context.Wait(this.MessageReceivedAsync);
            }
        }
    }
}