using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Http.ModelBinding;
using System.Web.Mvc;
using System.Web.Security;
using System.Data.Entity;
using ITechETradeBook_v2.Areas.Admin.Models;
using ITechETradeBook_v2.Models;
using Microsoft.Ajax.Utilities;
using System.Data;
using System.IO;

namespace ITechETradeBook_v2.Areas.Admin.Controllers
{

    public class AccountController : Controller
    {
        #region
        etradebook_Entities db = new etradebook_Entities();
        CustomMemberShip memProvider = new CustomMemberShip();
        ITechETradeBook_v2.Controllers.HelpersController helpers = new ITechETradeBook_v2.Controllers.HelpersController();

        // GET: /Admin/
        public ActionResult LogOn()
        {
            return View();
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();
            Session["LastLogin"] = null;
            return RedirectToAction("LogOn", "Account");
        }

        // POST: /Admin/
        [HttpPost]
        public ActionResult LogOn(LogOnModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                if (memProvider.ValidateUser(model.Email, model.Password))
                {
                    FormsAuthentication.SetAuthCookie(model.Email, model.RememberMe);

                    var userLogin = db.Users.SingleOrDefault(w => w.Email == model.Email);
                    Session["LastLogin"] = userLogin.LastLogin;
                    userLogin.LastLogin = DateTime.Now;
                    db.SaveChanges();

                    if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1 && returnUrl.StartsWith("/")
                        && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        return RedirectToAction("Index", "Dashboard");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "The user name or password provided is incorrect.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Admin/Account/Members
        [Authorize(Roles = "Administrator")]
        public ActionResult Members()
        {
            return View();
        }

        public JsonResult GetNumOfMembers()
        {
            var membership = from m in db.Memberships.Include(m => m.User).Include(m => m.SystemLookup)
                            //orderby b.BookingStatus ascending
                            select m;

            return Json(new
            {
                Count = membership.Count()
            }, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        public JsonResult LoadMembeResult(jQueryDataTableParamModel param)
        {
            var membership = from m in db.Memberships.Include(m => m.User).Include(m => m.SystemLookup)
                             //orderby b.BookingStatus ascending
                             where m.IsATempMember == false
                             select m;
            if (!string.IsNullOrEmpty(param.sSearch))
            {
                membership = membership.Where(m => m.BusinessName.ToLower().Contains(param.sSearch.ToLower())
                                                     || m.MembershipNumber.ToLower().Contains(param.sSearch.ToLower()));
            }

            List<ITechETradeBook_v2.Models.Membership> listMembership = membership.ToList();
            //List<MemberShipModel> listMembership = new List<MemberShipModel>();
            //foreach (ITechETradeBook_v2.Models.Membership mMemberships in membership)
            //{
            //    MemberShipModel mMemberShipModel = new MemberShipModel();
            //    mMemberShipModel.UserID = mMemberships.UserId;
            //    mMemberShipModel.MembershipNo = mMemberships.MembershipNumber;
            //    mMemberShipModel.FullName = mMemberships.ContactName;
            //    mMemberShipModel.MembershipType = mMemberships.SystemLookup.Name;
            //    mMemberShipModel.Email = mMemberships.User.Email;
            //    mMemberShipModel.ranking = mMemberships.RankingValue;
            //    listMembership.Add(mMemberShipModel);
            //}

            var isMemberNoSortable = Convert.ToBoolean(Request["bSortable_0"]);
            var isFullNameSortable = Convert.ToBoolean(Request["bSortable_1"]);
            var isRankingSortable = Convert.ToBoolean(
                Request["bSortable_4"]);
            var sortColumnIndex = Convert.ToInt32(Request["iSortCol_0"]);

            //Func<ITechETradeBook_v2.Models.Membership, string> orderFunc = (m => sortColumnIndex == 0 && isMemberNoSortable ? m.MembershipNumber.ToString() :
            //                                            sortColumnIndex == 1 && isFullNameSortable ? m.BusinessName :
            //                                            sortColumnIndex == 4 && isRankingSortable ? m.RankingValue.ToString() : "");

            

            var sortDir = Request["sSortDir_0"];
            //if (sortDir == "asc")
            //{
                //(System.Linq.IOrderedEnumerable<BookingJSON>)
                listMembership = listMembership.OrderByDescending(m=>m.Created).ToList();
            //}
            //else
            //{
            //    //(System.Linq.IOrderedEnumerable<BookingJSON>)
            //    listMembership = listMembership.OrderByDescending(orderFunc).ToList();
            //}

            var listPagingMember = listMembership.Skip(param.iDisplayStart).Take(param.iDisplayLength);

            var listPagingmembers = listPagingMember.Select(m => new
            {
                UserID = m.UserId,
                MembershipNo = m.MembershipNumber,
                FullName = m.BusinessName,
                MembershipType = m.SystemLookup.Name,
                Email = m.Email,//m.User.Email,
                ranking = m.RankingValue,
                CreatedDate = m.Created.ToString("yyyy/MM/dd"),
                Status = m.User.StatusId,
                StatusType = m.MembershipTypeId
            });

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = listPagingmembers.Count(),
                iTotalDisplayRecords = listMembership.Count(),
                aaData = listPagingmembers
            }, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        public JsonResult ChangeMembershipType(int id)
        {
            int result = 0;
            try
            {
                ITechETradeBook_v2.Models.Membership membership = db.Memberships.SingleOrDefault(o => o.UserId == id);
                if (membership.MembershipTypeId == 6)
                {
                    membership.MembershipTypeId = 7;
                    result = 7;
                }
                else if (membership.MembershipTypeId == 7)
                {
                    membership.MembershipTypeId = 6;
                    result = 6;
                }
                db.SaveChanges();
            }
            catch (Exception e)
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
            //Tra ve ham success cua client de xu li
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        public JsonResult ChangeMembershipStatus(int id)
        {
            int result = 0;
            try
            {
                ITechETradeBook_v2.Models.Membership membership = db.Memberships.SingleOrDefault(o => o.UserId == id);
                if (membership.User.StatusId == 1)
                {
                    membership.User.StatusId = 3;
                    result = 3;
                }
                else if (membership.User.StatusId == 3)
                {
                    membership.User.StatusId = 1;
                    result = 1;
                }
                db.SaveChanges();
            }
            catch (Exception e)
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
            //Tra ve ham success cua client de xu li
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        //
        // GET: /Admin/Account/Uers

        [Authorize(Roles = "Administrator")]
        public ActionResult Users()
        {
            return View();
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult LoadUserResult(jQueryDataTableParamModel param)
        {
            var users = from m in db.Users.Include(m => m.Profile)
                        //orderby b.BookingStatus ascending
                        select m;
            if (!string.IsNullOrEmpty(param.sSearch))
            {
                users = users.Where(m => m.Email.ToLower().Contains(param.sSearch.ToLower())
                                                     || m.Profile.LastName.ToLower().Contains(param.sSearch.ToLower())
                                                     || m.Profile.FirstName.ToLower().Contains(param.sSearch.ToLower()));
            }

            List<UserAdminModel> listUsers = new List<UserAdminModel>();
            foreach (User user in users)
            {
                UserAdminModel userAdminModel = new UserAdminModel();
                userAdminModel.Id = user.Id;
                userAdminModel.strEmail = user.Email;
                userAdminModel.strFirstName = user.Profile.FirstName;
                userAdminModel.strLastName = user.Profile.LastName;
                var userRoles = from uR in db.UserRoles where uR.UserId == user.Id select uR;
                int intNumber = userRoles.Count();
                string[] listRoles = new string[intNumber];
                int numberUserRoles = 0;
                foreach (UserRole userRole in userRoles)
                {
                    listRoles[numberUserRoles] = userRole.Role.Name;
                    numberUserRoles++;
                }
                userAdminModel.Role = listRoles;
                userAdminModel.strBirthday = user.Profile.BirthDay.ToString();
                userAdminModel.strStatus = user.SystemLookup.Name;
                listUsers.Add(userAdminModel);
            }

            var isEmailSortable = Convert.ToBoolean(Request["bSortable_0"]);
            var isFirstNameSortable = Convert.ToBoolean(Request["bSortable_1"]);
            var isLastNameSortable = Convert.ToBoolean(Request["bSortable_2"]);
            var isStatusSortable = Convert.ToBoolean(Request["bSortable_5"]);
            var sortColumnIndex = Convert.ToInt32(Request["iSortCol_0"]);

            Func<UserAdminModel, string> orderFunc = (m => sortColumnIndex == 0 && isEmailSortable ? m.strEmail :
                                                        sortColumnIndex == 1 && isFirstNameSortable ? m.strFirstName :
                                                        sortColumnIndex == 2 && isLastNameSortable ? m.strLastName :
                                                        sortColumnIndex == 5 && isStatusSortable ? m.strStatus : "");

            var sortDir = Request["sSortDir_0"];
            if (sortDir == "asc")
            {
                //(System.Linq.IOrderedEnumerable<BookingJSON>)
                listUsers = listUsers.OrderBy(orderFunc).ToList();
            }
            else
            {
                //(System.Linq.IOrderedEnumerable<BookingJSON>)
                listUsers = listUsers.OrderByDescending(orderFunc).ToList();
            }

            var listPagingUser = listUsers.Skip(param.iDisplayStart).Take(param.iDisplayLength);

            return Json(new
            {
                sEcho = param.sEcho,
                iTotalRecords = listPagingUser.Count(),
                iTotalDisplayRecords = listUsers.Count(),
                aaData = listPagingUser
            }, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult Role()
        {
            var Roles = from r in db.Roles select r;
            return View(Roles);
        }

        [Authorize(Roles = "Administrator")]
        public JsonResult GetRole(int id)
        {
            Role role = new Role();
            try
            {
                role = db.Roles.Single(o => o.Id == id);
            }
            catch (Exception e)
            {
                return Json(null, JsonRequestBehavior.AllowGet);
            }
            return Json(new string[3] { role.Name, role.Description, role.SystemLookup.Name }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetMembershipDetail(string memberNumber)
        {

            MemberShipModel member = new MemberShipModel();
            try
            {
                ITechETradeBook_v2.Models.Membership membership = db.Memberships.Single(o => o.MembershipNumber == memberNumber);
                member.Member = membership;
                member.MembershipType = membership.SystemLookup.Name;
                var categoryMem = db.MembershipCategories.Where(mc => mc.UserId == membership.UserId);
                string membershipCate = "";
                foreach (MembershipCategory mc in categoryMem)
                {
                    membershipCate = membershipCate +
                                     db.CategoryCultures.Single(cc => cc.CategoryId == mc.CategoryId).Name + ", ";
                }

                try
                {
                    member.CategoryMembership = membershipCate.Substring(0, membershipCate.Length - 3);
                }
                catch
                {
                    member.CategoryMembership = "";
                }
                member.BirthDay = membership.DateOfBirth.ToString();
                member.CountryName = membership.Country.Name;
                member.ProvinceName = membership.Province.Name;


                try
                {
                    member.DistrictName = membership.District.Name;
                }
                catch 
                {
                    member.DistrictName = "";
                }

                try
                {
                    member.BusinessArea = db.CategoryCultures.Single(cc => cc.CategoryId == membership.BusinessAreaId).Name;
                }
                catch
                {
                    member.BusinessArea = "";
                }
                member.CreatedDate = membership.Created.ToString();
                member.LastModifiedDate = membership.LastModified.ToString();
                member.GoldExpire = membership.Gold_Expiry_Date.ToString();
            }
            catch (Exception e)
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
            return Json(new string[]
            {
                member.Member.Logo,member.Member.ContactName,member.Member.ContactNumber,
                member.Member.Email,member.BirthDay,member.Member.Tel,member.Member.Fax,
                member.DistrictName,member.ProvinceName,member.CountryName,member.Member.PostalAddress,
                member.Member.BalanceCreditPoint.ToString(),member.Member.MembershipNumber,member.Member.BusinessName,
                member.Member.JobTitle,member.BusinessArea,member.CategoryMembership,member.Member.WebsiteAddress,member.Member.Department,
                member.CreatedDate,member.LastModifiedDate,member.MembershipType,member.GoldExpire,member.Member.RankingValue.ToString(),
                member.Member.PageViewed.ToString(),member.Member.Description
            }, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult EditRole(int id, string name, string description, string active)
        {
            Role role = db.Roles.Single(r => r.Id == id);
            try
            {
                role.Name = name;
                role.LowerName = name.ToLower();
                role.Description = description;
                if (active == "on")
                {
                    role.StatusId = 4;
                }
                else
                {
                    role.StatusId = 5;
                }
                db.SaveChanges();
            }
            catch (Exception e)
            {
                TempData["failRole"] = "Edit role fail.";
                return RedirectToAction("Role", "Account");
            }
            TempData["successRole"] = "Edit role successful.";
            return RedirectToAction("Role", "Account");
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult CreateRole(string name, string description, string active)
        {
            Role role = new Role();
            try
            {
                role.Name = name;
                role.LowerName = name.ToLower();
                role.Description = description;
                if (active == "on")
                {
                    role.StatusId = 4;
                }
                else
                {
                    role.StatusId = 5;
                }
                db.Roles.Add(role);
                db.SaveChanges();
            }
            catch (Exception e)
            {
                TempData["failRole"] = "Create new role fail.";
                return RedirectToAction("Role", "Account");
            }
            TempData["successRole"] = "Create new role successful.";
            return RedirectToAction("Role", "Account");
        }

        [Authorize(Roles = "Administrator")]
        public JsonResult getUser(int id)
        {
            User user = new User();
            try
            {
                user = db.Users.SingleOrDefault(o => o.Id == id);
            }
            catch (Exception e)
            {
                return Json(null, JsonRequestBehavior.AllowGet);
            }
            return Json(user.Email, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        public JsonResult changeStatusUser(int id)
        {
            int intResult = 0;
            try
            {
                User user = db.Users.SingleOrDefault(o => o.Id == id);
                if (user.StatusId == 2)
                {
                    user.StatusId = 1;
                }
                else if (user.StatusId == 1)
                {
                    user.StatusId = 3;
                }
                else
                {
                    user.StatusId = 1;
                }
                intResult = user.StatusId;
                db.SaveChanges();
            }
            catch (Exception e)
            {
                return Json(0, JsonRequestBehavior.AllowGet);
            }
            //Tra ve ham success cua client de xu li
            return Json(intResult, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        public JsonResult changePassword(int id)
        {
            try
            {
                User user = db.Users.SingleOrDefault(o => o.Id == id);
                StringBuilder sb = new StringBuilder();
                char c;
                Random random = new Random();
                for (int i = 0; i < 8; i++)
                {
                    c = Convert.ToChar(Convert.ToInt32(random.Next(65, 87)));
                    sb.Append(c);
                }
                string newPass = sb.ToString().ToLower();
                user.Password = memProvider.HashPassword(newPass.Trim());
                user.LastModified = DateTime.Now;
                db.SaveChanges();
                SendConfirm(user.Profile.FirstName, newPass.Trim(), user.Email);
            }
            catch (Exception e)
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
            //Tra ve ham success cua client de xu li
            return Json(true, JsonRequestBehavior.AllowGet);
        }

        [Authorize(Roles = "Administrator")]
        private void SendConfirm(string cust_Name, string newPassword, string receive)
        {
            string email = ConfigurationManager.AppSettings["SystemEmail"].ToString();
            string pass = ConfigurationManager.AppSettings["SystemEmailPassword"].ToString();
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress("customerservice@e-tradebook.com");
            mail.To.Add(new MailAddress(receive));

            mail.Subject = "[e-TradeBook.com] Changing Your Password";
            mail.SubjectEncoding = System.Text.Encoding.UTF8;
            mail.IsBodyHtml = true;
            string bodyHTML = "<table cellspacing='0' cellpadding='0' border='0' style='background-color: #f0f7fc; border: 1px solid #a5cae4;'>";
            bodyHTML += "<tbody>";
            bodyHTML += "<tr>";
            bodyHTML += "<td style='background-color: #d7edfc; padding: 5px 10px; border-bottom: 1px solid #a5cae4; font-family: 'Trebuchet MS', Helvetica, Arial, sans-serif; font-size: 11px; line-height: 1.231;'>";
            bodyHTML += "<a rel='nofollow' style='color: #176093; text-decoration: none;'>e-TradeBook</a>";
            bodyHTML += "</td>";
            bodyHTML += "</tr>";
            bodyHTML += "<tr>";
            bodyHTML += "<td style='background-color: #fcfcff; padding: 1em; color: #141414; font-family: 'Trebuchet MS', Helvetica, Arial, sans-serif; font-size: 13px; line-height: 1.231;'>";
            bodyHTML += "<strong>Dear " + cust_Name + ",</strong>";
            bodyHTML += "<p style='margin-top: 10px;'>Welcome to E-TRADEBOOK, your password was changed. And your new password is <b style='font-size:17px'>" + newPassword + "</b>.</p>";
            bodyHTML += "<p>Thanks,<br>e-TradeBook - Onine Business Network</p>";
            bodyHTML += "</td>";
            bodyHTML += "</tr>";
            bodyHTML += "<tr>";
            bodyHTML += "<td style='background-color: #f0f7fc; padding: 5px 10px; border-top: 1px solid #d7edfc; text-align: right; font-family: 'Trebuchet MS', Helvetica, Arial, sans-serif; font-size: 11px; line-height: 1.231;'>";
            bodyHTML += "<a rel='nofollow' style='color: #176093; text-decoration: none;'>" + Request.Url.GetLeftPart(UriPartial.Authority).ToString() + "</a>";
            bodyHTML += "</td></tr></tbody></table>";

            mail.Body = bodyHTML;
            //Create a new instance of SMTP client and pass name and port number         
            //of the smtp gmail server 
            SmtpClient tSmtpClient = new SmtpClient("mail.e-tradebook.com", 26);

            //Enable SSL of the SMTP client
            //tSmtpClient.EnableSsl = true;
            //Use delivery method as network
            tSmtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            //Use DefaultCredentials set to false
            tSmtpClient.UseDefaultCredentials = false;
            //Pass account information of the sender
            tSmtpClient.Credentials = new System.Net.NetworkCredential(email, pass);
            tSmtpClient.Send(mail);
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult EditUser(int? id)
        {
            User user = db.Users.SingleOrDefault(u => u.Id == id);
            var userRole = from u in db.UserRoles where u.UserId == id select u;
            Profile profile = db.Profiles.SingleOrDefault(p => p.UserId == id);
            ViewBag.user = user;
            ViewBag.userRole = userRole;
            ViewBag.profile = profile;
            return View();
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult UpdateUser(string v_UserId, IEnumerable<string> v_ckbRole, string v_firstName, string v_lastName,
            string v_day, string v_month, string v_year, string v_gender)
        {
            int userID = int.Parse(v_UserId);
            User userGet = new User();
            try
            {
                // su ly table UserRole

                var userRole = from ur in db.UserRoles where ur.UserId == userID select ur;
                userGet = db.Users.SingleOrDefault(u => u.Id == userID);
                foreach (string s in v_ckbRole)
                {
                    bool flagAdd = false;
                    int roleId = int.Parse(s);
                    foreach (UserRole role in userRole)
                    {
                        if (role.RoleId == roleId)
                        {
                            flagAdd = true;
                            break;
                        }
                    }
                    if (flagAdd == false)
                    {
                        UserRole ur = new UserRole();
                        ur.UserId = userID;
                        ur.RoleId = roleId;
                        db.UserRoles.Add(ur);
                        db.SaveChanges();
                    }
                }

                foreach (UserRole role in userRole.ToList())
                {
                    bool flagDelete = false;
                    int roleId = 0;
                    foreach (string s in v_ckbRole)
                    {
                        roleId = int.Parse(s);
                        if (roleId == role.RoleId)
                        {
                            flagDelete = true;
                            break;
                        }
                    }
                    if (flagDelete == false)
                    {
                        db.UserRoles.Remove(role);
                        db.SaveChanges();
                    }
                }

                // su ly tren table Profile
                Profile profile = db.Profiles.SingleOrDefault(p => p.UserId == userID);
                profile.FirstName = v_firstName;
                profile.LastName = v_lastName;
                string strDate = v_day + "-" + v_month + "-" + v_year;
                DateTime date = Convert.ToDateTime(strDate);
                profile.BirthDay = date;
                profile.GenderId = int.Parse(v_gender);
                db.SaveChanges();
            }
            catch (Exception e)
            {
                TempData["failAlert"] = "Update user is NOT successfull!";
                return RedirectToAction("EditUser", "Account");
            }
            TempData["successAlert"] = "Update user " + userGet.Email + " is successfull!";
            return RedirectToAction("Users", "Account");
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult CreateUser()
        {
            return View();
        }
        #endregion
        public ActionResult CopyMemberFromDB()
        {
            return View();
        }

        [HttpPost]
        public ActionResult CopyMemberFromDB(DBImport bu, ITechETradeBook_v2.Models.Membership member, User u, Profile pro, FormCollection data)
        {
            var numberOfMember = data["numMember"];
            ViewBag.numOfMem = numberOfMember;
            int i_numberOfMember = Convert.ToInt32(numberOfMember);

            List<DBImport> dbimportList = GetDBImportData(i_numberOfMember);
            for (int i = 0; i < dbimportList.Count; i++)
            {
                if (CheckExistEmail(dbimportList[i].Email, 0) == false)
                {
                    if (dbimportList[i].LocationId == 114) // Australia first
                    {
                        string passWord = dbimportList[i].Email.Split('@')[0];
                        int id = InsertUser(dbimportList[i].Email, passWord, 1);

                        if (id != -1)
                        {
                            InsertProfiles(id, dbimportList[i].CompanyName, dbimportList[i].CompanyName, 1, 2);
                            InsertUserRole(id);
                            string contactNumber = dbimportList[i].M_Phone != null ? dbimportList[i].M_Phone : "";
                            string Tel = dbimportList[i].O_Phone != null ? dbimportList[i].O_Phone : "";
                            int LId = 13;
                            if (dbimportList[i].LocationId != null)
                            {
                                LId = Convert.ToInt32(dbimportList[i].LocationId);
                            }

                            if (dbimportList[i].CategoryId != null)
                            {
                                int CategoryId = Convert.ToInt32(dbimportList[i].CategoryId);
                                InsertMembershipCategory(id, CategoryId);
                            }
                            InsertMemberShip(id, dbimportList[i].CompanyName, contactNumber, LId, Tel, dbimportList[i].Website, dbimportList[i].Email, dbimportList[i].Address);
                            InsertMembershipRanking(id);
                        }
                    }
                }
            }
            return View();
        }

        private List<DBImport> GetDBImportData(int numofRecord)
        {
            var query = (from m in db.DBImports
                         select m).Take(numofRecord).ToList();

            return query;
        }

        private int InsertUser(string email, string password, int statusId)
        {
            User account = new ITechETradeBook_v2.Models.User();
            int returnValue = -1;
            account.Email = email;
            account.LowerEmail = email.ToLower();
            account.Password = memProvider.HashPassword(password.Trim());
            account.Created = DateTime.Now;
            account.LastModified = DateTime.Now;
            account.StatusId = statusId;
            account.TypeCreate = 2;
            try
            {
                db.Users.Add(account);                 
                db.SaveChanges();       
                returnValue = (Int32)account.Id;
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                // Retrieve the error messages as a list of strings.
                List<string> errorMessages = new List<string>();
                foreach (var e in ex.EntityValidationErrors)
                {
                    //check the ErrorMessage property
                    string entityName = e.Entry.Entity.GetType().Name;
                    foreach (System.Data.Entity.Validation.DbValidationError error in e.ValidationErrors)
                    {
                        errorMessages.Add(entityName + "." + error.PropertyName + ": " + error.ErrorMessage);
                    }
                }


            }
            return returnValue;
        }

        private bool InsertMembershipCategory(int userId, int CategoryId)
        {
            MembershipCategory MC = new ITechETradeBook_v2.Models.MembershipCategory();
            MC.UserId = userId;
            MC.CategoryId = CategoryId;

            try
            {
                db.MembershipCategories.Add(MC);
                db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }

        }
        private bool InsertUserRole(int userid)
        {
            UserRole role = new ITechETradeBook_v2.Models.UserRole();
            role.UserId = userid;
            role.RoleId = 3;
            try
            {
                db.UserRoles.Add(role);
                db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InsertProfiles(int UserId, string firstName, string lastName, int GenderId, int CustomerTypeId)
        {
            Profile _profile = new ITechETradeBook_v2.Models.Profile();

            _profile.UserId = UserId;
            _profile.FirstName = firstName;
            _profile.LastName = lastName;
            _profile.GenderId = GenderId;
            _profile.CustomerTypeId = CustomerTypeId;

            try
            {
                db.Profiles.Add(_profile);
                db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InsertMembershipRanking(int userid)
        {
            MembershipRanking mranking = new MembershipRanking();
            mranking.UserId = userid;
            mranking.GenuineMember = true;
            mranking.InformationAccuracy = false;
            mranking.PagedViewUpTo10000 = false;
            mranking.PagedViewMoreThan10000 = false;
            mranking.MemberMoreThanOneYear = false;
            mranking.TradeOnWebsite = false;
            mranking.ValueOfTradeAbove50000 = false;
            mranking.ValueOfTradeAbove100000 = false;
            mranking.ActiveMember = false;
            mranking.CustomerSatisfactionAndReputation = false;

            try
            {
                db.MembershipRankings.Add(mranking);
                // Save First Ranking for membership
                db.SaveChanges();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void InsertMemberShip(int userid, string businessName, string contactNo, int? locationId, string Tel, string Website, string eMail, string address)
        {
            var acc_Membership = new ITechETradeBook_v2.Models.Membership();

            acc_Membership.UserId = userid;

            acc_Membership.BusinessName = businessName;
            acc_Membership.ContactName = businessName;
            acc_Membership.ContactNumber = contactNo;
            acc_Membership.BusinessAreaId = 932;
            acc_Membership.LocationId = (Int32)locationId;
            acc_Membership.ProvinceId = 1;
            acc_Membership.DistrictId = 1;
            acc_Membership.PostalAddress = address != null ? address : "";
            acc_Membership.JobTitle = "";
            acc_Membership.Department = "";
            acc_Membership.Tel = Tel;
            acc_Membership.WebsiteAddress = Website;
            /**** Static Value of Membership ****/
            acc_Membership.DateOfBirth = DateTime.Now;
            acc_Membership.Email = eMail;
            acc_Membership.LastModified = DateTime.Now;
            acc_Membership.Created = DateTime.Now;
            acc_Membership.MembershipTypeId = 6; // 6: Free membership
            acc_Membership.RankingValue = 1; // After fill Required SignUp ranking value is 1
            acc_Membership.PageViewed = 0;
            acc_Membership.BalanceCreditPoint = Convert.ToDecimal(0);
            acc_Membership.IsDisplayContactNumber = false;
            acc_Membership.IsDisplayDateOfBirth = false;
            acc_Membership.IsDisplayPostalAddress = false;

            /***** Generate MemebershipCode****/
            string strMembershipNumber = "";
            var countryCode = (from c in db.Countries where c.Id == acc_Membership.LocationId select c.CountryCode).FirstOrDefault().ToString();
            // MemberShip Number = 12 digits: 2 (Country Code) + 2 (Business AreaID) + Membership ID
            string strCodeId = acc_Membership.UserId.ToString().PadLeft(8, '0');

            strMembershipNumber = countryCode + acc_Membership.BusinessAreaId.ToString() + strCodeId;
            acc_Membership.MembershipNumber = strMembershipNumber;
            
            //default cover photo
            acc_Membership.CoverPhoto = "e-tradebookcover.jpg";
            acc_Membership.Logo = "e-tradebooklogo_default.jpg";

            db.Memberships.Add(acc_Membership);
            db.SaveChanges();
            //return true;
        }
        #region
        [Authorize(Roles = "Administrator")]
        public ActionResult CreateNewUser(string v_UserId, string v_email, string v_password, IEnumerable<string> v_ckbRole, string v_firstName, string v_lastName,
            string v_day, string v_month, string v_year, string v_gender, string v_CustomerType)
        {
            if (CheckExistEmail(v_email, 0) == false)
            {
                try
                {
                    // su ly table UserRole
                    User account = new ITechETradeBook_v2.Models.User();
                    Profile acc_Profile = new Profile();

                    // Save Account to Table User
                    account.Email = v_email;
                    account.LowerEmail = v_email.ToLower();
                    account.Password = memProvider.HashPassword(v_password);
                    account.StatusId = 2;
                    // User status is SystemLookupType id = 1 and value of status in table SystemLookup
                    account.Created = DateTime.Now;
                    account.LastModified = DateTime.Now;
                    db.Users.Add(account);
                    db.SaveChanges();

                    // Create Role for User Register
                    foreach (string s in v_ckbRole.ToList())
                    {
                        UserRole role = new UserRole();
                        role.UserId = account.Id;
                        role.RoleId = int.Parse(s);
                        db.UserRoles.Add(role);
                        db.SaveChanges();
                    }

                    // Save information to Table Profile
                    string strDOB = v_day + "-" + v_month + "-" + v_year;
                    DateTime dtDOB = Convert.ToDateTime(strDOB);
                    acc_Profile.UserId = account.Id;
                    acc_Profile.FirstName = v_firstName;
                    acc_Profile.LastName = v_lastName;
                    acc_Profile.BirthDay = dtDOB;
                    acc_Profile.GenderId = int.Parse(v_gender);
                    acc_Profile.CustomerTypeId = int.Parse(v_CustomerType);
                    // CustTypeID view value in table lookup Culture. Value: 4,5,6
                    acc_Profile.LastModified = DateTime.Now;
                    db.Profiles.Add(acc_Profile);
                    db.SaveChanges();
                }
                catch (Exception e)
                {
                    TempData["failAlert"] = "Create new user is NOT successfull!";
                    return RedirectToAction("CreateUser", "Account");
                }
            }
            else
            {
                TempData["failAlert"] = "The email has already used!";
                return RedirectToAction("CreateUser", "Account");
            }
            TempData["successAlert"] = "Create new user " + v_email + " is successfull!";
            return RedirectToAction("Users", "Account");
        }

        [Authorize(Roles = "Administrator")]
        private bool CheckExistEmail(string email, int id)
        {
            try
            {
                if (id == 0)
                {
                    if (db.Users.Any(m => m.Email.Equals(email)))
                    {
                        return true;
                    }
                }
                else
                {
                    string EmailExist = db.Users.Single(m => m.Id == id).Email;
                    if (db.Users.Any(m => m.Email.Equals(email)))
                    {
                        if (EmailExist.Trim().ToLower() != email.Trim().ToLower())
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            return false;
        }

        [Authorize(Roles = "Administrator")]
        public ActionResult MEdit(string id)
        {
            ITechETradeBook_v2.Models.Membership member = db.Memberships.Where(w => w.MembershipNumber == id).FirstOrDefault();
            return View(member);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult MEdit(ITechETradeBook_v2.Models.Membership member, HttpPostedFileBase imgLogoPhoto, HttpPostedFileBase imgCoverPhoto)
        {
            if (ModelState.IsValid)
            {
                var existedMember = db.Memberships.Single(m => m.UserId == member.UserId);

                if (existedMember == null)
                    return View(member);

                existedMember.BusinessAreaId = Convert.ToInt32(Request["ddlcategory"]);//member.BusinessAreaId;
                existedMember.BusinessName = member.BusinessName;
                existedMember.ContactName = member.ContactName;
                existedMember.ContactNumber = member.ContactNumber;
                existedMember.Department = member.Department;
                existedMember.Description = member.Description;
                existedMember.DistrictId = member.DistrictId;
                existedMember.Email = member.Email;
                existedMember.JobTitle = member.JobTitle;
                existedMember.LastModified = DateTime.Now;
                existedMember.LocationId = member.LocationId;
                existedMember.MembershipTypeId = member.MembershipTypeId;
                existedMember.PostalAddress = member.PostalAddress;
                existedMember.ProvinceId = member.ProvinceId;
                existedMember.Tel = member.Tel;
                existedMember.Fax = member.Fax;
                existedMember.WebsiteAddress = member.WebsiteAddress;
                db.SaveChanges();

                //upload logo and cover images
                //logo
                // get Ourbrand to save image
                if(imgLogoPhoto != null && imgLogoPhoto.ContentLength > 0)
                {
                    Stream inputStream = imgLogoPhoto.InputStream;
                    MemoryStream memory = inputStream as MemoryStream;
                    if (memory == null)
                    {
                        memory = new MemoryStream();
                        inputStream.CopyTo(memory);
                    }
                    byte[] bImage = memory.ToArray();

                    string strImageUrl = Convert.ToBase64String(bImage);
                    // Get filename of uploadLogo
                    var fileName = imgLogoPhoto.FileName;
                    helpers.UploadPhoto(imgLogoPhoto, fileName, "/Images/Logos/");
                    existedMember.Logo = fileName;
                    db.SaveChanges();
                }
                

                //cover
                if(imgCoverPhoto != null && imgCoverPhoto.ContentLength > 0)
                {
                    Stream inputStreamCover = imgCoverPhoto.InputStream;
                    MemoryStream memoryCover = inputStreamCover as MemoryStream;
                    if (memoryCover == null)
                    {
                        memoryCover = new MemoryStream();
                        inputStreamCover.CopyTo(memoryCover);
                    }
                    byte[] bImageCover = memoryCover.ToArray();
                    // Get filename of uploadLogo
                    var fileNameCover = imgCoverPhoto.FileName;
                    helpers.UploadPhoto(imgCoverPhoto, fileNameCover, "/Images/Covers/");
                    existedMember.CoverPhoto = fileNameCover;
                    db.SaveChanges();
                }


                //Save Membership Category
                var oldProductServices = db.MembershipCategories.Where(m => m.UserId == existedMember.UserId);
                foreach (var item in oldProductServices)
                {
                    db.MembershipCategories.Remove(item);
                }

                if (Request["ProductService"] != null)
                {
                    var arrProductService = Request["ProductService"].Split(',');
                    foreach (var item in arrProductService)
                    {
                        if (item != string.Empty)
                        {
                            var obMemberCate = new MembershipCategory();
                            obMemberCate.UserId = member.UserId;
                            obMemberCate.CategoryId = Convert.ToInt32(item);
                            db.MembershipCategories.Add(obMemberCate);
                        }
                    }
                }
                db.SaveChanges();
                return Redirect(Request.UrlReferrer.ToString());
            }

            return View(member);
        }

        #endregion
    }
}
