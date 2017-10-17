using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using DotNetOpenAuth.AspNet;
using Microsoft.Web.WebPages.OAuth;
using WebMatrix.WebData;
using System.Net.Mail;
using System.IO;
using System.Text;
using PagedList;
using System.Drawing;
using System.Web.Helpers;
using System.Configuration;
using System.Net;
using Newtonsoft.Json;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using ITechETradeBook_v2.Models;
using ITechETradeBook_v2.Utility;
using ITechETradeBook_v2.Services;
using System.Text.RegularExpressions;

namespace ITechETradeBook_v2.Controllers
{
    //[InitializeSimpleMembership]
    public class AccountController : Controller
    {
        AccountService _accountService = new AccountService();
        etradebook_Entities _db = new etradebook_Entities();
        CustomMemberShipProvider memProvider = new CustomMemberShipProvider();
        CustomRoleMembership roleProvider = new CustomRoleMembership();
        HelpersController helpers = new HelpersController();
        VirtualMarketController cVirtualMarket = new VirtualMarketController();
        Status status = new Status();
        SystemConfig config = new SystemConfig();
        string WEBURL = System.Web.HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);

        // path folder Cover Image
        string strCoverPath = System.Web.HttpContext.Current.Server.MapPath("~/Images/Covers/");

        #region Constant Variable

        private const int WALLTYPE_PHOTO = 14;
        private const int WALLTYPE_NOTES = 15;
        private const int WALLTYPE_EVENTS = 16;
        private const int WALLPUBLISHTYPE_SHARE = 17;
        private const int WALLPUBLISHTYPE_PRIVATE = 18;
        private const int WALL_PHOTO_WIDTH = 480;
        private const int WALL_PHOTO_HEIGHT = 360;

        private const int GOLD_MEMBER_TYPE_ID = 7;

        #endregion

        public ActionResult Index()
        {
            return View();
        }

        #region Main
        /************* Custome by me *****************/
        // GET
        [Authorize]
        public ActionResult Edit(int? id)
        {
            var user = (from u in _db.Users where u.Id == id select u).FirstOrDefault();
            return View(user);
        }

        // GET
        /// <summary>
        /// View for change Password
        /// </summary>
        /// <returns></returns>
        public ActionResult Password(int? id)
        {
            var user = (from u in _db.Users where u.Id == id select u).FirstOrDefault();
            return View(user);
        }

        // POST
        [HttpPost]
        public ActionResult Password(int? id, FormCollection form)
        {
            // Get value from Form
            string newPass = form["password"];
            string confirm = form["confirm"];

            // Logic content
            if (newPass == confirm) // Match password
            {
                if (newPass.Length >= 8) // valid lenght of Password
                {
                    string passHashed = memProvider.HashPassword(newPass.Trim());
                    var user = (from u in _db.Users where u.Id == id select u).FirstOrDefault();

                    user.Password = passHashed;
                    _db.SaveChanges();

                    return RedirectToAction("Manage", "Account");
                }
                else
                {
                    return View();
                }
            }
            else
            {
                return View();
            }
        }
        /************* Custome by me *****************/
        //
        // GET: /Account/Login

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }
        }

        //
        // POST: /Account/Login

        [HttpPost]
        [AllowAnonymous]
        public ActionResult Login(LoginModel model, string returnUrl)
        {
            if (ModelState.IsValid && memProvider.ValidateUser(model.Email, model.Password) == true)
            {
                FormsAuthentication.SetAuthCookie(model.Email, true);

                var userLogin = _db.Users.SingleOrDefault(w => w.Email == model.Email);
                userLogin.LastLogin = DateTime.Now;
                _db.SaveChanges();

                var isProfessional = _db.LookupCultures.SingleOrDefault( x => x.LanguageId == 1 && x.Name.Equals("Professional")).LookupId == userLogin.Profile.CustomerTypeId;

                if (CheckSignUpRequired() == false)
                {
                    if (isProfessional)
                        return Redirect("ProfessionalFirstSignUp");
                    
                    return RedirectToAction("FProfile", "Account");
                }
                else
                {
                    if (isProfessional)
                        return Redirect("ProfessionalProfile");

                    return RedirectToAction("BusinessProfile", "Account");
                }
            }
            else
            {
                TempData["warning"] = "Username and password is not valid.";
                return RedirectToAction("Login", "Account");
            }
        }

        //
        // POST: /Account/LogOff
        [Authorize]
        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();

            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Account/Register

        [AllowAnonymous]
        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                PageCulture condition = _db.PageCultures.Single(p => p.Title == "Terms and Conditions");
                ViewBag.pageCondition = condition;
                return View();
            }
        }

        //
        // POST: /Account/Register

        /// <summary>
        /// Action user to create new account for system by register page in front end.
        /// Request["agree"] is value of check box "I'v read and agreed on Condations" in View and "agree" is name of checkbox
        /// </summary>
        /// <param name="model">User model to sycn data from form in view to action</param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public ActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                // Check is click agree
                if (Request["agree"] == "on")  // Check checked agree Conditions
                {
                    if (memProvider.GetUserNameByEmail(model.Email.Trim()) == "") // Check exist account email
                    {
                        // Attempt to register the user
                        try
                        {
                            User account = new ITechETradeBook_v2.Models.User();
                            Profile acc_Profile = new Models.Profile();
                            //ITechETradeBook_v2.Models.Membership acc_Membership = new Models.Membership();

                            //Check & get customer type
                            var culture = _db.LookupCultures.SingleOrDefault(x => x.Name.Equals(model.CustomerType) && x.LanguageId == 1);
                            if (culture == null)
                            {
                                PageCulture condition = _db.PageCultures.Single(p => p.Title == "Terms and Conditions");
                                ViewBag.pageCondition = condition;
                                return View();
                            }

                            // Save Account to Table User
                            account.Email = model.Email.Trim();
                            account.LowerEmail = model.Email.ToLower();
                            account.Password = memProvider.HashPassword(model.Password.Trim());
                            account.StatusId = 2; // User status is SystemLookupType id = 1 and value of status in table SystemLookup
                            account.Created = DateTime.Now;
                            account.LastModified = DateTime.Now;
                            account.TypeCreate = 1;
                            _db.Users.Add(account);
                            _db.SaveChanges();

                            // Create Role for User Register
                            UserRole role = new Models.UserRole();
                            role.UserId = account.Id;
                            role.RoleId = 3;
                            _db.UserRoles.Add(role);
                            _db.SaveChanges();

                            // Save information to Table Profile
                            //string strDOB = model.inputBirthday_Day + "-" + model.inputBirthday_Month + "-" + model.inputBirthday_Year;
                            //DateTime dtDOB = Convert.ToDateTime(strDOB);
                            acc_Profile.UserId = account.Id;
                            acc_Profile.FirstName = model.FirstName.Trim();
                            acc_Profile.LastName = model.LastName.Trim();
                            acc_Profile.BirthDay = model.Birthday;
                            acc_Profile.GenderId = model.GenderId;
                            acc_Profile.CustomerTypeId = culture.LookupId;
                            
                            acc_Profile.LastModified = DateTime.Now;
                            _db.Profiles.Add(acc_Profile);
                            _db.SaveChanges();

                            // Save First Ranking for membership
                            MembershipRanking mranking = new MembershipRanking();
                            mranking.UserId = acc_Profile.UserId;
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
                            _db.MembershipRankings.Add(mranking);
                            // Save First Ranking for membership
                            _db.SaveChanges();
                            TempData["success"] = status.strRegisterSuccess;

                            // Send confirmation email
                            var isProfessional = culture.Name.Equals("Professional");
                            string userName = acc_Profile.FirstName + " " + acc_Profile.LastName;
                            Guid verifyKey = Guid.NewGuid();
                            string verifyUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/Account/VerifyAccount/" + account.Id + "?verifyKey=" + verifyKey;
                            _accountService.SendConfirmationEmail(userName, account.Email, verifyUrl, isProfessional);

                            // Save log Email 
                            // Create New Log System Email
                            SystemEmail oSignupEmail = new SystemEmail();
                            oSignupEmail.Id = verifyKey;
                            oSignupEmail.UserId = account.Id;
                            oSignupEmail.EmailTypeId = 34; // 34: Mean email type is: Signup Email
                            oSignupEmail.Created = DateTime.Now;

                            _db.SystemEmails.Add(oSignupEmail);
                            _db.SaveChanges();

                            return RedirectToAction("Login", "Account");
                        }
                        catch (MembershipCreateUserException e)
                        {
                            ModelState.AddModelError("", ErrorCodeToString(e.StatusCode));
                        } // end try catch
                    }
                    else
                    {
                        TempData["warning"] = status.strWarRegisterDuplicate;
                        return RedirectToAction("Register", "Account");
                    }// end check exist email account
                }
                else
                {
                    TempData["warning"] = status.strWarRegisterNotAgree;
                    return RedirectToAction("Register", "Account");
                } // end check checked checkbox "I'v read and agreed on Condations"
            }

            // If we got this far, something failed, redisplay form
            return View();
        }

        public ActionResult Accept(string verifyKey, int? id)
        {
            // Check is exist that verify code
            Guid gVerifyKey = Guid.Parse(verifyKey.Trim());
            var oSysEmail = (from se in _db.SystemEmails
                             where se.Id == gVerifyKey && se.UserId == 1 && se.EmailTypeId == 34
                             select se).FirstOrDefault();
            if (oSysEmail != null)
            {
                // Get Import data
                var business = (from ip in _db.DBImports
                                where ip.ID == id && (ip.IsActive == false || ip.IsActive == null)
                                select ip).FirstOrDefault();

                if (business != null)
                {
                    // Check exist User with business Email
                    var member = (from m in _db.Users
                                  where m.Email == business.Email
                                  select m);

                    if (member.Count() < 1 || member == null)
                    {
                        // Create account for business
                        User account = new ITechETradeBook_v2.Models.User();
                        Profile acc_Profile = new Models.Profile();

                        // Save Account to Table User
                        account.Email = business.Email.Trim();
                        account.LowerEmail = business.Email.ToLower();

                        /** GENERATE PASS **/
                        StringBuilder sb = new StringBuilder();
                        char c;
                        Random random = new Random();
                        for (int i = 0; i < 8; i++)
                        {
                            c = Convert.ToChar(Convert.ToInt32(random.Next(65, 87)));
                            sb.Append(c);
                        }
                        string newPass = sb.ToString().ToLower();
                        /** GENERATE PASS **/

                        account.Password = memProvider.HashPassword(newPass);
                        account.StatusId = 1; // User status is SystemLookupType id = 1 and value of status in table SystemLookup
                        account.Created = DateTime.Now;
                        account.LastModified = DateTime.Now;
                        _db.Users.Add(account);
                        _db.SaveChanges();

                        // Create Role for User Register
                        UserRole role = new Models.UserRole();
                        role.UserId = account.Id;
                        role.RoleId = 3;
                        _db.UserRoles.Add(role);
                        _db.SaveChanges();

                        // Save information to Table Profile
                        //string strDOB = model.inputBirthday_Day + "-" + model.inputBirthday_Month + "-" + model.inputBirthday_Year;
                        //DateTime dtDOB = Convert.ToDateTime(strDOB);
                        acc_Profile.UserId = account.Id;
                        acc_Profile.FirstName = business.FirstName.Trim();
                        acc_Profile.LastName = business.LastName.Trim();
                        acc_Profile.BirthDay = DateTime.Now;
                        acc_Profile.GenderId = 1;
                        acc_Profile.CustomerTypeId = 6; // CustTypeID view value in table lookup Culture. Value: 4,5,6
                        acc_Profile.LastModified = DateTime.Now;
                        _db.Profiles.Add(acc_Profile);
                        _db.SaveChanges();

                        // Save First Ranking for membership
                        MembershipRanking mranking = new MembershipRanking();
                        mranking.UserId = acc_Profile.UserId;
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
                        _db.MembershipRankings.Add(mranking);
                        // Save First Ranking for membership
                        _db.SaveChanges();

                        // Send Email Account Information
                        string EmailContent = "Dear " + business.CompanyName + ",<br /> We are very happy and thank you for your attention. We created for you an account to join to our system. Please use below accoutn information and <a href='" + Request.Url.GetLeftPart(UriPartial.Authority) + "'>Login</a> system to complete your detail information and start your business. We hope you will happy with <a href='" + Request.Url.GetLeftPart(UriPartial.Authority) + "'>E-tradebook</a>.<br /> Account Information: <br /> Email: " + account.Email + "<br />Password: " + newPass + "<br />Thank,<br />E-TradeBook - Onine Business Network";
                        helpers.SystemSendEmail(EmailContent, account.Email, "Account Information");

                        TempData["success"] = "<strong>Thank you</strong> Your account information had sent to Email: " + business.Email + ". Please check it.";
                        business.IsActive = true;
                        business.ModifiedDate = DateTime.Now;
                        _db.SaveChanges();
                        return RedirectToAction("Login", "Account");
                    }
                    else
                    {
                        TempData["warning"] = "Your information was registed.";
                        return RedirectToAction("Login", "Account");
                    }
                }
                else
                {
                    TempData["warning"] = "Your information not exist or registed.";
                    return RedirectToAction("Login", "Account");
                }
            }
            else
            {
                TempData["warning"] = "Your information not exist.";
                return RedirectToAction("Login", "Account");
            }
        }

        public ActionResult VerifyAccount(string verifyKey, int? id)
        {
            // Get User have id
            var oMember = (from m in _db.Users
                           where m.Id == id
                           select m).FirstOrDefault();

            // Check status of Member
            if (oMember != null)
            {
                if (oMember.StatusId == 2) // 2 mean status is Confirm
                {
                    try
                    {
                        // Check verifyCode is valid
                        Guid gVerifyKey = Guid.Parse(verifyKey.Trim());
                        var oSysEmail = (from se in _db.SystemEmails
                                         where se.Id == gVerifyKey && se.UserId == id && se.EmailTypeId == 34
                                         select se).FirstOrDefault();

                        if (oSysEmail != null)
                        {
                            TempData["success"] = status.strVerifySuccess;

                            // Update status
                            oMember.StatusId = 1;
                            _db.SaveChanges();
                        }
                        else
                        {
                            TempData["warning"] = status.strWarVerifyUnsuccess;
                        }
                        return RedirectToAction("Login", "Account");
                    }
                    catch (Exception)
                    {
                        return RedirectToAction("Error", "Home");
                    }
                }
                else if (oMember.StatusId == 1) // User had actived
                {
                    // Set status and redirect to Login Page
                    TempData["warning"] = status.strWarDuplicateVerify;
                    // Check logged User to show status
                    if (User.Identity.IsAuthenticated) // If logged
                    {
                        // Logout User and redirect to Login Page
                        FormsAuthentication.SignOut();
                    }
                    return RedirectToAction("Login", "Account");
                }
                else // 3: Mean account locked
                {
                    return RedirectToAction("Error", "Home");
                }
            }
            else
            {
                return RedirectToAction("Error", "Home");
            }
        }

        [Authorize]
        public ActionResult FProfile()
        {
            if (CheckSignUpRequired() == true)
            {
                return RedirectToAction("BusinessProfile", "Account");
            }
            else
            {
                // Check exist account in DBImport Data
                var dbImport = (from dta in _db.DBImports
                                where dta.Email == WebSecurity.CurrentUserName
                                select dta).FirstOrDefault();

                return View(dbImport);
            }
        }

        private int GetRoleOf(long uid)
        {
            var role = (from ur in _db.UserRoles
                        where ur.UserId == uid
                        select ur).FirstOrDefault();

            return role.RoleId;
        }

        [Authorize]
        [ValidateInput(false)]
        [HttpPost]
        public ActionResult FProfile(Models.Membership model, HttpPostedFileBase uploadLogo, HttpPostedFileBase uploadCover, HttpPostedFileBase uploadProductServicePhoto,
            HttpPostedFileBase uploadProductServicePhoto1, HttpPostedFileBase uploadProductServicePhoto2, HttpPostedFileBase uploadProductServicePhoto3, MarketDisplay mdMarket, MarketDisplayCulture mdMarketCulture)
        {
            // Get User current Login 
            var user = memProvider.GetUserByEmail(User.Identity.Name);
            // Save information of current member ship
            // Get curent user logged.
            var acc_Membership = new Models.Membership();

            if (ModelState.IsValid)
            {
                try
                {
                    // Save Membership
                    #region Save Membership
                    acc_Membership.UserId = user.Id;
                    if (model.BusinessName != null)
                    {
                        acc_Membership.BusinessName = model.BusinessName.Trim();
                    }

                    if (model.ContactName != null)
                    {
                        acc_Membership.ContactName = model.ContactName.Trim();
                    }

                    if (model.ContactNumber != null)
                    {
                        acc_Membership.ContactNumber = model.ContactNumber.Trim();
                    }

                    if (model.BusinessAreaId != 0)
                    {
                        acc_Membership.BusinessAreaId = model.BusinessAreaId;
                    }

                    if (model.LocationId != 0)
                    {
                        acc_Membership.LocationId = model.LocationId;
                    }

                    if (model.ProvinceId != null)
                    {
                        acc_Membership.ProvinceId = model.ProvinceId;
                    }

                    if (model.DistrictId != null)
                    {
                        acc_Membership.DistrictId = model.DistrictId;
                    }

                    if (model.PostalAddress != null)
                    {
                        acc_Membership.PostalAddress = model.PostalAddress.Trim();
                    }

                    if (model.JobTitle != null)
                    {
                        acc_Membership.JobTitle = model.JobTitle.Trim();
                    }

                    if (model.Department != null)
                    {
                        acc_Membership.Department = model.Department.Trim();
                    }

                    if (model.Tel != null)
                    {
                        acc_Membership.Tel = model.Tel.Trim();
                    }

                    if (model.WebsiteAddress != null)
                    {
                        acc_Membership.WebsiteAddress = model.WebsiteAddress.Trim();
                    }

                    #endregion

                    /**** Static Value of Membership ****/
                    #region Static Value of Membership
                    acc_Membership.DateOfBirth = DateTime.Now;
                    acc_Membership.Email = user.Email;
                    acc_Membership.LastModified = DateTime.Now;
                    acc_Membership.Created = DateTime.Now;
                    acc_Membership.MembershipTypeId = 6; // 6: Free membership
                    acc_Membership.RankingValue = 1; // After fill Required SignUp ranking value is 1
                    acc_Membership.PageViewed = 0;
                    acc_Membership.BalanceCreditPoint = Convert.ToDecimal(0);
                    acc_Membership.IsDisplayContactNumber = false;
                    acc_Membership.IsDisplayDateOfBirth = false;
                    acc_Membership.IsDisplayPostalAddress = false;
                    #endregion
                    /**** Static Value of Membership ****/
                    /***** Generate MemebershipCode****/
                    string strMembershipNumber = "";
                    var countryCode = (from c in _db.Countries where c.Id == acc_Membership.LocationId select c.CountryCode).FirstOrDefault().ToString();
                    // MemberShip Number = 12 digits: 2 (Country Code) + 2 (Business AreaID) + Membership ID
                    string strCodeId = acc_Membership.UserId.ToString().PadLeft(8, '0');

                    strMembershipNumber = countryCode + acc_Membership.BusinessAreaId.ToString() + strCodeId;
                    acc_Membership.MembershipNumber = strMembershipNumber;
                    /***** Generate MemebershipCode****/
                    // After set all field save to db
                    _db.Memberships.Add(acc_Membership);
                    _db.SaveChanges();

                    // Upload 2 photo to server
                    // Upload Logo photo if has
                    /*** noted: Not yet check duplicate name of photo and delete old photo */
                    #region 
                    if (uploadLogo != null && uploadLogo.ContentLength > 0)
                    {
                        if ((uploadLogo.ContentLength / 1024 / 1024) <= 2) // size valid
                        {
                            // Get filename of uploadLogo
                            //var fileNameLogo = uploadLogo.FileName;
                            Guid strGuid = Guid.NewGuid();
                            string fileNameLogo = strGuid.ToString() + "." + uploadLogo.FileName.Split('.').LastOrDefault().ToString();
                            helpers.UploadPhoto(uploadLogo, fileNameLogo, config.strLogoImagesPath);

                            // Save file name of image to database
                            acc_Membership.Logo = fileNameLogo;
                        }
                        else
                        {
                            TempData["FProfilewarning"] = status.strInvalidFileSize;
                            return RedirectToAction("FProfile", "Account");
                        }
                    }

                    #endregion
                    // Upload Cover photo if has
                    /*** noted: Not yet check duplicate name of photo and delete old photo */
                    #region
                    if (uploadCover != null && uploadCover.ContentLength > 0)
                    {
                        if ((uploadCover.ContentLength / 1024 / 1024) <= 2) // size valid
                        {
                            // Get filename of uploadLogo
                            //var fileNameCover = uploadCover.FileName;
                            Guid strGuid = Guid.NewGuid();
                            string fileNameCover = strGuid.ToString() + "." + uploadCover.FileName.Split('.').LastOrDefault().ToString();
                            helpers.UploadPhoto(uploadCover, fileNameCover, config.strCoverImagesPath);

                            // Save file name of image to database
                            acc_Membership.CoverPhoto = fileNameCover;
                        }
                        else
                        {
                            TempData["FProfilewarning"] = status.strInvalidFileSize;
                            return RedirectToAction("FProfile", "Account");
                        }
                    }
                    #endregion
                    // Update Member to 1 start in database
                    var memRanking = (from mr in _db.MembershipRankings where mr.UserId == acc_Membership.UserId select mr).FirstOrDefault();
                    memRanking.InformationAccuracy = true;
                    // Save model
                    _db.SaveChanges();

                    /***** Save Membership Category *****/
                    #region
                    var arrProductService = Request["ProductService"].Split(',');
                    foreach (var item in arrProductService)
                    {
                        if (item != "")
                        {
                            var obMemberCate = new MembershipCategory();
                            obMemberCate.UserId = acc_Membership.UserId;
                            obMemberCate.CategoryId = Convert.ToInt32(item);
                            _db.MembershipCategories.Add(obMemberCate);
                            _db.SaveChanges();
                        }
                    }
                    #endregion
                    /***** Save Membership Category *****/

                    /***** Create First Free MarketDisplay for FreeMember *****/
                    // If member not upload any photo (Logo, Cover, and Main Photo)
                    #region
                    if (!((uploadCover == null || uploadCover.ContentLength == 0) && (uploadLogo == null || uploadLogo.ContentLength == 0) && (uploadProductServicePhoto == null || uploadProductServicePhoto.ContentLength == 0)))
                    {
                        MarketDisplay obMarketDisplay = new MarketDisplay();

                        obMarketDisplay.MarketDisplayNumber = "";
                        obMarketDisplay.CategoryId = model.BusinessAreaId;
                        obMarketDisplay.LocationId = model.LocationId;
                        obMarketDisplay.ProvinceId = model.ProvinceId;
                        obMarketDisplay.DistrictId = model.DistrictId;
                        obMarketDisplay.CurrencyId = mdMarket.CurrencyId;
                        if (mdMarket.Price > 0)
                        {
                            obMarketDisplay.Price = mdMarket.Price;
                        }
                        else
                        {
                            obMarketDisplay.Price = Convert.ToDecimal(0);
                        }
                        obMarketDisplay.EffectiveFrom = DateTime.Now;
                        obMarketDisplay.IsSale = true;
                        obMarketDisplay.StatusId = 56; // Status is pending
                        obMarketDisplay.LastModified = DateTime.Now;
                        obMarketDisplay.UserId = acc_Membership.UserId;
                        obMarketDisplay.Created = DateTime.Now;
                        obMarketDisplay.IsUnlimited = true;

                        // Upload First MarketDisplay Photo Product and Service
                        /*** noted: Not yet check duplicate name of photo and delete old photo */
                        MarketDisplayPhoto obMarketDisplayPhoto = new MarketDisplayPhoto();

                        if (uploadProductServicePhoto != null && uploadProductServicePhoto.ContentLength > 0)
                        {
                            if ((uploadProductServicePhoto.ContentLength / 1024 / 1024) <= 2) // size valid
                            {
                                // Get filename of uploadLogo
                                //var fileNamePS = uploadProductServicePhoto.FileName;
                                Guid strGuid = Guid.NewGuid();
                                string fileNamePS = strGuid.ToString() + "." + uploadProductServicePhoto.FileName.Split('.').LastOrDefault().ToString();
                                helpers.UploadPhoto(uploadProductServicePhoto, fileNamePS, config.strVirtualMarketImagesPath);

                                // Save file name of image to database
                                obMarketDisplay.Photo = fileNamePS;
                                obMarketDisplayPhoto.Image = fileNamePS;
                            }
                            else
                            {
                                TempData["FProfilewarning"] = status.strInvalidFileSize;
                                return RedirectToAction("FProfile", "Account");
                            }
                        }
                        else // Check cover or logo to upload it to virtual market
                        {
                            // Logo first
                            if (uploadLogo != null || uploadLogo.ContentLength > 0)
                            {
                                if ((uploadLogo.ContentLength / 1024 / 1024) <= 2) // size valid
                                {
                                    // Get filename of uploadLogo
                                    //var fileNamePS = uploadLogo.FileName;
                                    Guid strGuid = Guid.NewGuid();
                                    string fileNamePS = strGuid.ToString() + "." + uploadLogo.FileName.Split('.').LastOrDefault().ToString();
                                    helpers.UploadPhoto(uploadLogo, fileNamePS, config.strVirtualMarketImagesPath);

                                    // Save file name of image to database
                                    obMarketDisplay.Photo = fileNamePS;
                                    obMarketDisplayPhoto.Image = fileNamePS;
                                }
                                else
                                {
                                    TempData["FProfilewarning"] = status.strInvalidFileSize;
                                    return RedirectToAction("FProfile", "Account");
                                }
                            }
                            else
                            {
                                if ((uploadCover.ContentLength / 1024 / 1024) <= 2) // size valid
                                {
                                    // Get filename of uploadLogo
                                    //var fileNamePS = uploadCover.FileName;
                                    Guid strGuid = Guid.NewGuid();
                                    string fileNamePS = strGuid.ToString() + "." + uploadCover.FileName.Split('.').LastOrDefault().ToString();
                                    helpers.UploadPhoto(uploadCover, fileNamePS, config.strVirtualMarketImagesPath);

                                    // Save file name of image to database
                                    obMarketDisplay.Photo = fileNamePS;
                                    obMarketDisplayPhoto.Image = fileNamePS;
                                }
                                else
                                {
                                    TempData["FProfilewarning"] = status.strInvalidFileSize;
                                    return RedirectToAction("FProfile", "Account");
                                }
                            }
                        }

                        cVirtualMarket.CreateMarketDisplay(obMarketDisplay);

                        obMarketDisplayPhoto.MarketDisplayId = obMarketDisplay.Id;
                        cVirtualMarket.CreateMarketDisplayPhoto(obMarketDisplayPhoto);

                        // MARKET DISPLAY CULTURES
                        MarketDisplayCulture obMarketDisplayCulture = new MarketDisplayCulture();
                        obMarketDisplayCulture.MarketDisplayId = obMarketDisplay.Id;
                        obMarketDisplayCulture.LanguageId = 1;//_db.Languages.Where(w => w.CountryId == obMarketDisplay.LocationId).FirstOrDefault().Id;
                        if (mdMarketCulture.ProductName != null)
                        {
                            obMarketDisplayCulture.ProductName = mdMarketCulture.ProductName;
                            obMarketDisplayCulture.Url = Helper.GenerateSlug(mdMarketCulture.ProductName.ToLower()) + "-" + obMarketDisplay.Id.ToString();
                        }
                        else
                        {
                            obMarketDisplayCulture.ProductName = acc_Membership.BusinessName;
                            obMarketDisplayCulture.Url = Helper.GenerateSlug(acc_Membership.BusinessName.ToLower()) + "-" + obMarketDisplay.Id.ToString();
                        }

                        if (mdMarketCulture.Description != null)
                        {
                            obMarketDisplayCulture.Description = mdMarketCulture.Description.Trim();
                        }
                        else
                        {
                            obMarketDisplayCulture.Description = "";
                        }
                        cVirtualMarket.CreateMarketDisplayCulture(obMarketDisplayCulture);

                        // CREATE MARKETDISPLAY LIMIT
                        MarketDisplayLimit obMarketDisplayLimit = new MarketDisplayLimit();
                        obMarketDisplayLimit.UserId = acc_Membership.UserId;
                        obMarketDisplayLimit.Year = DateTime.Now.Year;
                        obMarketDisplayLimit.FreeMember = 1;
                        obMarketDisplayLimit.GoldMember = 0;
                        obMarketDisplayLimit.TotalPhoto = 1;
                        _db.MarketDisplayLimits.Add(obMarketDisplayLimit);
                        _db.SaveChanges();

                        // CREATE MARKETDISPLAY PHOTOS
                        if (uploadProductServicePhoto1 != null && uploadProductServicePhoto1.ContentLength > 0)
                        {
                            if ((uploadProductServicePhoto1.ContentLength / 1024 / 1024) <= 2) // size valid
                            {
                                // Get filename of uploadLogo
                                //var fileNamePS = uploadProductServicePhoto1.FileName;
                                Guid strGuid = Guid.NewGuid();
                                string fileNamePS = strGuid.ToString() + "." + uploadProductServicePhoto1.FileName.Split('.').LastOrDefault().ToString();
                                helpers.UploadPhoto(uploadProductServicePhoto1, fileNamePS, config.strVirtualMarketImagesPath);

                                // Create new record MarketDisplay Photo
                                MarketDisplayPhoto obMarketDisplayChildPhoto = new MarketDisplayPhoto();
                                obMarketDisplayChildPhoto.MarketDisplayId = obMarketDisplay.Id;
                                obMarketDisplayChildPhoto.Image = fileNamePS;
                                cVirtualMarket.CreateMarketDisplayPhoto(obMarketDisplayChildPhoto);
                            }
                            else
                            {
                                TempData["FProfilewarning"] = status.strInvalidFileSize;
                                return RedirectToAction("FProfile", "Account");
                            }
                        }

                        if (uploadProductServicePhoto2 != null && uploadProductServicePhoto2.ContentLength > 0)
                        {
                            if ((uploadProductServicePhoto2.ContentLength / 1024 / 1024) <= 2) // size valid
                            {
                                // Get filename of uploadLogo
                                //var fileNamePS = uploadProductServicePhoto2.FileName;
                                Guid strGuid = Guid.NewGuid();
                                string fileNamePS = strGuid.ToString() + "." + uploadProductServicePhoto2.FileName.Split('.').LastOrDefault().ToString();
                                helpers.UploadPhoto(uploadProductServicePhoto2, fileNamePS, config.strVirtualMarketImagesPath);

                                // Create new record MarketDisplay Photo
                                MarketDisplayPhoto obMarketDisplayChildPhoto = new MarketDisplayPhoto();
                                obMarketDisplayChildPhoto.MarketDisplayId = obMarketDisplay.Id;
                                obMarketDisplayChildPhoto.Image = fileNamePS;
                                cVirtualMarket.CreateMarketDisplayPhoto(obMarketDisplayChildPhoto);
                            }
                            else
                            {
                                TempData["FProfilewarning"] = status.strInvalidFileSize;
                                return RedirectToAction("FProfile", "Account");
                            }
                        }

                        if (uploadProductServicePhoto3 != null && uploadProductServicePhoto3.ContentLength > 0)
                        {
                            if ((uploadProductServicePhoto3.ContentLength / 1024 / 1024) <= 2) // size valid
                            {
                                // Get filename of uploadLogo
                                //var fileNamePS = uploadProductServicePhoto3.FileName;
                                Guid strGuid = Guid.NewGuid();
                                string fileNamePS = strGuid.ToString() + "." + uploadProductServicePhoto3.FileName.Split('.').LastOrDefault().ToString();
                                helpers.UploadPhoto(uploadProductServicePhoto3, fileNamePS, config.strVirtualMarketImagesPath);

                                // Create new record MarketDisplay Photo
                                MarketDisplayPhoto obMarketDisplayChildPhoto = new MarketDisplayPhoto();
                                obMarketDisplayChildPhoto.MarketDisplayId = obMarketDisplay.Id;
                                obMarketDisplayChildPhoto.Image = fileNamePS;
                                cVirtualMarket.CreateMarketDisplayPhoto(obMarketDisplayChildPhoto);
                            }
                            else
                            {
                                TempData["FProfilewarning"] = status.strInvalidFileSize;
                                return RedirectToAction("FProfile", "Account");
                            }
                        }
                    }
                    /***** Create First Free MarketDisplay for FreeMember *****/

                    TempData["FProfilesuccess"] = status.strUpdateProfileSuccess;
                    return RedirectToAction("BusinessProfile", "Account");
                }
                catch (Exception)
                {
                    TempData["FProfilewarning"] = status.strUpdateProfileUnsuccess;
                    return RedirectToAction("FProfile", "Account");
                }
            }
            else
            {
                TempData["FProfilewarning"] = status.strUpdateProfileUnsuccess;
                return RedirectToAction("FProfile", "Account");
            }
                    #endregion
        }

        public ActionResult BusinessProfile(string id)
        {
            //visitors who can go directly member profile
            if (CheckSignUpRequired() == false)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    ViewData["IsOtherMemberWall"] = true;
                    

                    //ChiNguyen: when id is a friendly url. This is used for SEO
                    string[] words = id.Split('-');
                    string lastItem = words.LastOrDefault();

                    if (!string.IsNullOrEmpty(lastItem))
                    {
                        id = lastItem;
                    }
                    else
                    {
                        return RedirectToAction("Error", "Home");
                    }

                    
                    
                    

                    var member = (from u in _db.Memberships
                                  where u.MembershipNumber == id
                                  select u).FirstOrDefault();

                    if (member != null)
                    {
                        // Check with current logged to plus page view
                        // If selected member has email same with current name => no plus else will plus
                        if (member.Email != WebSecurity.CurrentUserName)
                        {
                            member.PageViewed = member.PageViewed + 1;
                            _db.SaveChanges();
                        }

                        var businessArea = (from c in _db.CategoryCultures
                                            where c.CategoryId == member.BusinessAreaId
                                            select c.Name).FirstOrDefault();
                        if (businessArea == null)
                        {
                              businessArea = "";
                        }

                        //Get Product/services
                        var Memcate = (from c in _db.MembershipCategories
                                       where c.UserId == member.UserId
                                       select c).ToList();
                        var listProduct = "";
                        foreach (var item in Memcate)
                        {
                            var cate = (from c in _db.CategoryCultures
                                        where c.CategoryId == item.CategoryId && c.LanguageId == 1
                                        select c.Name).FirstOrDefault();

                            listProduct += cate + ", ";
                        }


                    // Photo number value
                        ViewData["PhotoNumber"] = CountPhoto(member.UserId);
                        //Business Name
                        ViewData["BusinessName"] = member.BusinessName;
                        ViewData["subTitle"] = "Business Area : " + businessArea.Trim().ToString() + " - Product/Service : " + listProduct.Trim().TrimEnd(',').ToString();
                        ViewData["Description"] = Regex.Replace(member.Description, "<[^>]*>", string.Empty).Trim();



                        ////////////////////////////////////////////////////////////////////////////////////////
                        //Get Popular Items

                        var PhotoProfileNum = (from settingPhoto in _db.SettingPhotoes
                                               where settingPhoto.UserId == member.UserId
                                                      && settingPhoto.Photo != string.Empty
                                               select settingPhoto).ToList();
                        if (PhotoProfileNum.Count() != 0)
                        {
                            ViewBag.PopularItems = PhotoProfileNum;
                        }
                        else
                        {
                            ViewBag.PopularItems = new List<SettingPhoto>();
                        }

                        ///////////////////////////////////////////////////////////////////////


                        return View("BusinessProfileReadOnly", member);


                        
                    }
                    else
                    {
                        return RedirectToAction("Error", "Home");
                    }
                }
                else
                {
                    return RedirectToAction("FProfile", "Account");
                }
            }

            try
            {
                // VuongVT: Variable to detech is going to other member's wall
                if (!string.IsNullOrEmpty(id))
                {
                    ViewData["IsOtherMemberWall"] = true;

                    //ChiNguyen: when id is a friendly url. This is used for SEO
                    string[] words = id.Split('-');
                    string lastItem = words.LastOrDefault();

                    if (!string.IsNullOrEmpty(lastItem))
                    {
                        id = lastItem;
                    }
                    else
                    {
                        return RedirectToAction("Error", "Home");
                    }
                    
                }

                var member = (from u in _db.Memberships
                              where u.MembershipNumber == id
                              select u).FirstOrDefault();
                
                

                if (member != null) // Mean id not null
                {
                    // Check with current logged to plus page view
                    // If selected member has email same with current name => no plus else will plus
                    if (member.Email != WebSecurity.CurrentUserName)
                    {
                        member.PageViewed = member.PageViewed + 1;
                        _db.SaveChanges();
                    }

                    var currentUser = (from u in _db.Memberships
                                       where u.Email == WebSecurity.CurrentUserName
                                       select u).FirstOrDefault();

                    // Parameter to detech is Gold member or not
                    if (currentUser.MembershipTypeId.Equals(GOLD_MEMBER_TYPE_ID))
                    {
                        ViewData["IsGoldMember"] = true;
                    }
                    else
                    {
                        ViewData["IsGoldMember"] = false;
                    }

                    // Photo number value
                    ViewData["PhotoNumber"] = CountPhoto(member.UserId);
                    //Business Name
                    ViewData["BusinessName"] = member.BusinessName;



                    ////////////////////////////////////////////////////////////////////////////////////////
                    //Get Popular Items

                    var PhotoProfileNum = (from settingPhoto in _db.SettingPhotoes
                                           where settingPhoto.UserId == member.UserId
                                                  && settingPhoto.Photo != string.Empty
                                           select settingPhoto).ToList();
                    if (PhotoProfileNum.Count() != 0)
                    {
                        ViewBag.PopularItems = PhotoProfileNum;
                    }
                    else
                    {
                        ViewBag.PopularItems = new List<SettingPhoto>();
                    }

                    ///////////////////////////////////////////////////////////////////////


                    // Check is friend
                    var isFriend = _db.Friends.Any(friend => friend.UserId == currentUser.UserId && friend.FriendId == member.UserId);

                    // List Event - VuongVT: 06/28/2014  
                    List<EventCommentModel> lstEventCommentModel = new List<EventCommentModel>();

                    var listEvents = new List<ITechETradeBook_v2.Models.Wall>();

                    // List Comment of event - VuongVT: 07/01/2014    
                    var listCommentEvents = new List<ITechETradeBook_v2.Models.WallComment>();

                    // If is friend -> Load all event post with publishType is share
                    if (isFriend)
                    {
                        listEvents = (from wall in _db.Walls
                                      where wall.UserWall == member.UserId
                                            && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                            && wall.Events != null
                                      select wall).OrderByDescending(wall => wall.Id).ToList();

                        foreach (var item in listEvents)
                        {
                            EventCommentModel eventCommentModel = new EventCommentModel();
                            eventCommentModel.Event = item;

                            listCommentEvents = (from comment in _db.WallComments
                                                 where comment.WallId == item.Id
                                                       && comment.IsDeleted == false
                                                 select comment).OrderBy(comment => comment.Id).ToList();
                            eventCommentModel.ListComments = listCommentEvents;

                            lstEventCommentModel.Add(eventCommentModel);
                        }
                    }

                    // If wall of logged account, load all event
                    if (member.Email == WebSecurity.CurrentUserName)
                    {
                        listEvents = (from wall in _db.Walls
                                      where wall.UserWall == member.UserId
                                            && wall.Events != null
                                      select wall).OrderByDescending(wall => wall.Id).ToList();

                        foreach (var item in listEvents)
                        {
                            EventCommentModel eventCommentModel = new EventCommentModel();
                            eventCommentModel.Event = item;

                            listCommentEvents = (from comment in _db.WallComments
                                                 where comment.WallId == item.Id
                                                       && comment.IsDeleted == false
                                                 select comment).OrderBy(comment => comment.Id).ToList();
                            eventCommentModel.ListComments = listCommentEvents;

                            lstEventCommentModel.Add(eventCommentModel);
                        }
                        ViewData["IsOtherMemberWall"] = false;
                    }

                    ViewData["ListEvents"] = lstEventCommentModel;

                    // List Note - VuongVT: 06/28/2014 
                    List<NoteCommentModel> lstNoteCommentModel = new List<NoteCommentModel>();
                    var listNotes = new List<ITechETradeBook_v2.Models.Wall>();

                    // If is friend -> Load all event post with publishType is share
                    if (isFriend)
                    {
                        listNotes = (from wall in _db.Walls
                                     where wall.UserWall == member.UserId
                                           && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                           && wall.Notes != null
                                     select wall).OrderByDescending(wall => wall.Id).ToList();
                        foreach (var item in listNotes)
                        {
                            NoteCommentModel noteCommentModel = new NoteCommentModel();
                            noteCommentModel.Note = item;
                            var listCommentNote = (from comment in _db.WallComments
                                                   where comment.WallId == item.Id
                                                         && comment.IsDeleted == false
                                                   select comment).OrderBy(comment => comment.Id).ToList();
                            noteCommentModel.ListCommentNotes = listCommentNote;
                            lstNoteCommentModel.Add(noteCommentModel);
                        }
                    }

                    // If wall of logged account, load all event
                    if (member.Email == WebSecurity.CurrentUserName)
                    {
                        listNotes = (from wall in _db.Walls
                                     where wall.UserWall == member.UserId
                                           && wall.Notes != null
                                     select wall).OrderByDescending(wall => wall.Id).ToList();
                        foreach (var item in listNotes)
                        {
                            NoteCommentModel noteCommentModel = new NoteCommentModel();
                            noteCommentModel.Note = item;
                            var listCommentNote = (from comment in _db.WallComments
                                                   where comment.WallId == item.Id
                                                         && comment.IsDeleted == false
                                                   select comment).OrderBy(comment => comment.Id).ToList();
                            noteCommentModel.ListCommentNotes = listCommentNote;
                            lstNoteCommentModel.Add(noteCommentModel);
                        }
                        ViewData["IsOtherMemberWall"] = false;
                    }

                    ViewData["ListNotes"] = lstNoteCommentModel;

                    List<PhotoCommentModel> lstPhotoCommentModel = new List<PhotoCommentModel>();

                    // If is friend -> Load all event post with publishType is share
                    if (isFriend)
                    {
                        // Get list photos to load list photos after add a photo
                        var listPhotos = (from wall in _db.Walls
                                          where wall.UserWall == member.UserId
                                                && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                                && wall.Photo != null
                                          select wall).OrderByDescending(wall => wall.Id).ToList();
                        foreach (var photo in listPhotos)
                        {
                            PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                            photoCommentModel.Photo = photo;
                            var listCommentPhoto = (from comment in _db.WallComments
                                                    where comment.WallId == photo.Id
                                                          && comment.IsDeleted == false
                                                    select comment).OrderBy(comment => comment.Id).ToList();
                            photoCommentModel.ListCommentPhotos = listCommentPhoto;
                            lstPhotoCommentModel.Add(photoCommentModel);
                        }
                    }

                    // If wall of logged account, load all event
                    if (member.Email == WebSecurity.CurrentUserName)
                    {
                        var listPhotos = (from wall in _db.Walls
                                          where wall.UserWall == member.UserId
                                                && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                                && wall.Photo != null
                                          select wall).OrderByDescending(wall => wall.Id).ToList();
                        foreach (var photo in listPhotos)
                        {
                            PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                            photoCommentModel.Photo = photo;
                            var listCommentPhoto = (from comment in _db.WallComments
                                                    where comment.WallId == photo.Id
                                                          && comment.IsDeleted == false
                                                    select comment).OrderBy(comment => comment.Id).ToList();
                            photoCommentModel.ListCommentPhotos = listCommentPhoto;
                            lstPhotoCommentModel.Add(photoCommentModel);
                        }
                        ViewData["IsOtherMemberWall"] = false;
                    }
                    ViewData["ListPhotos"] = lstPhotoCommentModel;

                    return View(member);
                }
                else // Current Bu Profile page is owner profile
                {
                    member = (from u in _db.Memberships
                              where u.Email == WebSecurity.CurrentUserName
                              select u).FirstOrDefault();

                    if (member.MembershipTypeId.Equals(GOLD_MEMBER_TYPE_ID))
                    {
                        ViewData["IsGoldMember"] = true;
                    }
                    else
                    {
                        ViewData["IsGoldMember"] = false;
                    }

                    ViewData["PhotoNumber"] = CountPhoto(member.UserId);

                    // Get list events to load list event after add an event
                    List<EventCommentModel> lstEventCommentModel = new List<EventCommentModel>();
                    var listEvents = (from wall in _db.Walls
                                      where wall.UserWall == member.UserId
                                            && wall.Events != null
                                      select wall).OrderByDescending(wall => wall.Id).ToList();
                    // Get list comment
                    foreach (var item in listEvents)
                    {
                        EventCommentModel eventCommentModel = new EventCommentModel();
                        eventCommentModel.Event = item;

                        var listCommentEvents = (from comment in _db.WallComments
                                                 where comment.WallId == item.Id
                                                       && comment.IsDeleted == false
                                                 select comment).OrderBy(comment => comment.Id).ToList();
                        eventCommentModel.ListComments = listCommentEvents;

                        lstEventCommentModel.Add(eventCommentModel);
                    }

                    // List events -- VuongVT: 28/06/2014
                    ViewData["ListEvents"] = lstEventCommentModel;

                    // List Note - VuongVT: 06/28/2014 
                    List<NoteCommentModel> lstNoteCommentModel = new List<NoteCommentModel>();
                    // Get list notes to load list notes after add a note
                    var listNotes = new List<ITechETradeBook_v2.Models.Wall>();

                    listNotes = (from wall in _db.Walls
                                 where wall.UserWall == member.UserId
                                       && wall.Notes != null
                                 select wall).OrderByDescending(wall => wall.Id).ToList();

                    foreach (var item in listNotes)
                    {
                        NoteCommentModel noteCommentModel = new NoteCommentModel();
                        noteCommentModel.Note = item;
                        var listCommentNote = (from comment in _db.WallComments
                                               where comment.WallId == item.Id
                                                     && comment.IsDeleted == false
                                               select comment).OrderBy(comment => comment.Id).ToList();
                        noteCommentModel.ListCommentNotes = listCommentNote;
                        lstNoteCommentModel.Add(noteCommentModel);
                    }

                    ViewData["ListNotes"] = lstNoteCommentModel;

                    List<PhotoCommentModel> lstPhotoCommentModel = new List<PhotoCommentModel>();

                    // Get list photos to load list photos after add a photo
                    var listPhotos = (from wall in _db.Walls
                                      where wall.UserWall == member.UserId
                                            && wall.Photo != null
                                      select wall).OrderByDescending(wall => wall.Id).ToList();
                    foreach (var photo in listPhotos)
                    {
                        PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                        photoCommentModel.Photo = photo;
                        var listCommentPhoto = (from comment in _db.WallComments
                                                where comment.WallId == photo.Id
                                                      && comment.IsDeleted == false
                                                select comment).OrderBy(comment => comment.Id).ToList();
                        photoCommentModel.ListCommentPhotos = listCommentPhoto;
                        lstPhotoCommentModel.Add(photoCommentModel);
                    }
                    ViewData["ListPhotos"] = lstPhotoCommentModel;

                    ////////////////////////////////////////////////////////////////////////////////////////
                    //Get Popular Items

                    var PhotoProfileNum = (from settingPhoto in _db.SettingPhotoes
                                       where settingPhoto.UserId == member.UserId
                                              && settingPhoto.Photo != string.Empty
                                       select settingPhoto).ToList();
                    if (PhotoProfileNum.Count() != 0)
                    {
                        ViewBag.PopularItems = PhotoProfileNum;
                    }
                    else
                    {
                        ViewBag.PopularItems = new List<SettingPhoto>();
                    }
                    ///////////////////////////////////////////////////////////////////////
                    Boolean isothermemberwall = Convert.ToBoolean(ViewData["IsOtherMemberWall"]);
                    Boolean ismemberwall = false;
                    if (!isothermemberwall)
                    {
                        ismemberwall = true;
                    }
                    ViewData["IsMemberWall"] = ismemberwall;

                    return View(member);
                }
            }
            catch (Exception)
            {
                return RedirectToAction("Error", "Home");
            }
        }

        public ActionResult PhotoDetail(string id)
        {
            string lastItem = "";
            if (!string.IsNullOrEmpty(id))
            {
                string[] words = id.Split('-');
                lastItem = words.LastOrDefault();
            }
            int photo_id = -1;
            if (!string.IsNullOrEmpty(lastItem))
            {
                photo_id = Convert.ToInt32(lastItem.ToString().Trim());
            }

            var query = (from sp in _db.SettingPhotoes
                         where sp.Id == photo_id
                         select sp).FirstOrDefault();
            if (query != null)
            {
                ViewBag.TitlePhoto = query.titleOfPhoto;
                ViewBag.photo = query.Photo;
                ViewBag.photoDescription = query.descriptionPhoto;
            }
            else
            {
                ViewBag.Title = "";
                ViewBag.photo = null;
                ViewBag.photoDescription = "";
            }
            return View();
        }

        [Authorize]
        public ActionResult ProfessionalProfile(string id)
        {

            if (CheckSignUpRequired() == false)
	        {
                return RedirectToAction("ProfessionalFirstSignUp", "Account");
	        }

            try
            {
                var member = (from u in _db.Memberships
                                       where u.Email == WebSecurity.CurrentUserName
                                       select u).FirstOrDefault(); ;
                if (!string.IsNullOrEmpty(id))
                {
                    ViewData["IsOtherMemberWall"] = true;

                    string[] words = id.Split('-');
                    string lastItem = words.LastOrDefault();

                    if (!string.IsNullOrEmpty(lastItem))
                    {
                        id = lastItem;
                        member = (from u in _db.Memberships
                                  where u.MembershipNumber == id
                                  select u).FirstOrDefault();
                    }
                }


                string membername = string.Empty;
                var objProfile = _db.Profiles.Where(m => m.UserId == member.UserId).FirstOrDefault();
                membername = objProfile.FirstName;
                ViewBag.memberName = membername;
                ViewBag.memberSureName = objProfile.LastName;

                var currentUser = (from u in _db.Memberships
                                   where u.Email == WebSecurity.CurrentUserName
                                   select u).FirstOrDefault();

                // Parameter to detech is Gold member or not
                if (currentUser.MembershipTypeId.Equals(GOLD_MEMBER_TYPE_ID))
                {
                    ViewData["IsGoldMember"] = true;
                }
                else
                {
                    ViewData["IsGoldMember"] = false;
                }

                // Photo number value
                ViewData["PhotoNumber"] = CountPhoto(member.UserId);
                //Business Name
                ViewData["BusinessName"] = member.BusinessName;

                var isFriend = _db.Friends.Any(friend => friend.UserId == currentUser.UserId && friend.FriendId == member.UserId);
                
                List<EventCommentModel> eventComments = new List<EventCommentModel>();

                var events = new List<ITechETradeBook_v2.Models.Wall>();

                var listCommentEvents = new List<ITechETradeBook_v2.Models.WallComment>();

                // If is friend -> Load all event post with publishType is share
                if (isFriend)
                {
                    events = (from wall in _db.Walls
                                  where wall.UserWall == member.UserId
                                        && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                        && wall.Events != null
                                  select wall).OrderByDescending(wall => wall.Id).ToList();

                    foreach (var item in events)
                    {
                        EventCommentModel eventCommentModel = new EventCommentModel();
                        eventCommentModel.Event = item;

                        listCommentEvents = (from comment in _db.WallComments
                                             where comment.WallId == item.Id
                                                   && comment.IsDeleted == false
                                             select comment).OrderBy(comment => comment.Id).ToList();
                        eventCommentModel.ListComments = listCommentEvents;

                        eventComments.Add(eventCommentModel);
                    }
                }

                // If wall of logged account, load all event
                if (member.Email == WebSecurity.CurrentUserName)
                {
                    events = (from wall in _db.Walls
                                  where wall.UserWall == member.UserId
                                        && wall.Events != null
                                  select wall).OrderByDescending(wall => wall.Id).ToList();

                    foreach (var item in events)
                    {
                        EventCommentModel eventCommentModel = new EventCommentModel();
                        eventCommentModel.Event = item;

                        listCommentEvents = (from comment in _db.WallComments
                                             where comment.WallId == item.Id
                                                   && comment.IsDeleted == false
                                             select comment).OrderBy(comment => comment.Id).ToList();
                        eventCommentModel.ListComments = listCommentEvents;

                        eventComments.Add(eventCommentModel);
                    }
                    ViewData["IsOtherMemberWall"] = false;
                }

                ViewData["ListEvents"] = eventComments;

                // List Note - VuongVT: 06/28/2014 
                List<NoteCommentModel> lstNoteCommentModel = new List<NoteCommentModel>();
                var listNotes = new List<ITechETradeBook_v2.Models.Wall>();

                // If is friend -> Load all event post with publishType is share
                if (isFriend)
                {
                    listNotes = (from wall in _db.Walls
                                 where wall.UserWall == member.UserId
                                       && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                       && wall.Notes != null
                                 select wall).OrderByDescending(wall => wall.Id).ToList();
                    foreach (var item in listNotes)
                    {
                        NoteCommentModel noteCommentModel = new NoteCommentModel();
                        noteCommentModel.Note = item;
                        var listCommentNote = (from comment in _db.WallComments
                                               where comment.WallId == item.Id
                                                     && comment.IsDeleted == false
                                               select comment).OrderBy(comment => comment.Id).ToList();
                        noteCommentModel.ListCommentNotes = listCommentNote;
                        lstNoteCommentModel.Add(noteCommentModel);
                    }
                }

                // If wall of logged account, load all event
                if (member.Email == WebSecurity.CurrentUserName)
                {
                    listNotes = (from wall in _db.Walls
                                 where wall.UserWall == member.UserId
                                       && wall.Notes != null
                                 select wall).OrderByDescending(wall => wall.Id).ToList();
                    foreach (var item in listNotes)
                    {
                        NoteCommentModel noteCommentModel = new NoteCommentModel();
                        noteCommentModel.Note = item;
                        var listCommentNote = (from comment in _db.WallComments
                                               where comment.WallId == item.Id
                                                     && comment.IsDeleted == false
                                               select comment).OrderBy(comment => comment.Id).ToList();
                        noteCommentModel.ListCommentNotes = listCommentNote;
                        lstNoteCommentModel.Add(noteCommentModel);
                    }
                    ViewData["IsOtherMemberWall"] = false;
                }

                ViewData["ListNotes"] = lstNoteCommentModel;

                List<PhotoCommentModel> lstPhotoCommentModel = new List<PhotoCommentModel>();

                // If is friend -> Load all event post with publishType is share
                if (isFriend)
                {
                    // Get list photos to load list photos after add a photo
                    var listPhotos = (from wall in _db.Walls
                                      where wall.UserWall == member.UserId
                                            && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                            && wall.Photo != null
                                      select wall).OrderByDescending(wall => wall.Id).ToList();
                    foreach (var photo in listPhotos)
                    {
                        PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                        photoCommentModel.Photo = photo;
                        var listCommentPhoto = (from comment in _db.WallComments
                                                where comment.WallId == photo.Id
                                                      && comment.IsDeleted == false
                                                select comment).OrderBy(comment => comment.Id).ToList();
                        photoCommentModel.ListCommentPhotos = listCommentPhoto;
                        lstPhotoCommentModel.Add(photoCommentModel);
                    }
                }

                // If wall of logged account, load all event
                if (member.Email == WebSecurity.CurrentUserName)
                {
                    var listPhotos = (from wall in _db.Walls
                                      where wall.UserWall == member.UserId
                                            && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                            && wall.Photo != null
                                      select wall).OrderByDescending(wall => wall.Id).ToList();
                    foreach (var photo in listPhotos)
                    {
                        PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                        photoCommentModel.Photo = photo;
                        var listCommentPhoto = (from comment in _db.WallComments
                                                where comment.WallId == photo.Id
                                                      && comment.IsDeleted == false
                                                select comment).OrderBy(comment => comment.Id).ToList();
                        photoCommentModel.ListCommentPhotos = listCommentPhoto;
                        lstPhotoCommentModel.Add(photoCommentModel);
                    }
                    ViewData["IsOtherMemberWall"] = false;
                }
                ViewData["ListPhotos"] = lstPhotoCommentModel;

                #region Professional articles
                var articlesList = new List<ITechETradeBook_v2.Models.ProfessionalArticle>();

                if (member.Email == WebSecurity.CurrentUserName)
                {
                    articlesList = _db.ProfessionalArticles.Where(x => x.UserId == currentUser.UserId).ToList();
                }
                else
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        ViewData["IsOtherMemberWall"] = true;

                        string[] words = id.Split('-');
                        string lastItem = words.LastOrDefault();

                        if (!string.IsNullOrEmpty(lastItem))
                        {
                            articlesList = _db.ProfessionalArticles.Where(x => x.User.Membership.MembershipNumber == lastItem && x.IsViewed).ToList();
                        }
                    }  
                }
                
                ViewData["ArticlesList"] = articlesList;
                #endregion

                return View(member);

            }
            catch (Exception ex)
            {
                return RedirectToAction("Error", "Home");
            }
        }

        public PartialViewResult PartialPhotos(int? page, string id)
        {
            // Set up Paging
            int pageNumber = (page ?? 1);
            int pageSize = 8;
            // Set up Paging

            //// Get Account by Email
            var Account = (from m in _db.Memberships
                           where m.MembershipNumber == id
                           select m).FirstOrDefault();

            var listPhoto = (from p in _db.SettingPhotoes
                             where p.UserId == Account.UserId
                             select p).ToList();

            return PartialView("_PartialPhotos", listPhoto.ToPagedList(pageNumber, pageSize));
        }

        public PartialViewResult PartialBuNetworkList(string id)
        {
            List<Models.Membership> lBuNetwork = new List<Models.Membership>();
            try
            {
                var member = (from u in _db.Memberships
                              where u.MembershipNumber == id
                              select u).FirstOrDefault();

                if (member != null) // Mean address link of browse has Membership Number
                {
                    // Check if owner Member
                    if (member.Email == WebSecurity.CurrentUserName)
                    {
                        // Select list friend of current User
                        var lFriend = (from f in _db.Friends
                                       where f.UserId == member.UserId //&& f.StatusId == 9 // 9: Approved
                                       select f).ToList();

                        // Get profile of friend and add to list Friend
                        lBuNetwork = new List<Models.Membership>();
                        foreach (var item in lFriend)
                        {
                            var friend = (from f in _db.Memberships
                                          where f.UserId == item.FriendId //&& f.User.StatusId == 1
                                          select f).FirstOrDefault();
                            if (friend != null)
                            {
                                lBuNetwork.Add(friend);
                            }
                        }
                    }
                    else // If not owner
                    {
                        // Selet top 10 people has the same business Type
                        lBuNetwork = _db.Memberships.Where(w => w.BusinessAreaId == member.BusinessAreaId && w.UserId != member.UserId).OrderBy(o => Guid.NewGuid()).Take(10).ToList();
                    } // End check owner Business Profile
                }
                else
                {// End check NUll Select member with MemberShip Number
                    var current = _db.Memberships.Where(w => w.Email == WebSecurity.CurrentUserName).FirstOrDefault();
                    // Select list friend of current User
                    var lFriend = (from f in _db.Friends
                                   where f.UserId == current.UserId && f.StatusId == 9
                                   select f).ToList();

                    // Get profile of friend and add to list Friend
                    lBuNetwork = new List<Models.Membership>();
                    foreach (var item in lFriend)
                    {
                        var friend = (from f in _db.Memberships
                                      where f.UserId == item.FriendId && f.User.StatusId == 1
                                      select f).FirstOrDefault(); 
                        if (friend != null)
                        {
                            lBuNetwork.Add(friend);
                        }
                    }
                } // Else is sure Owner
            }
            catch (Exception)
            {
                TempData["ParBuWarning"] = "Can not load list business";
            }
            return PartialView("_PartialBuNetworkList", lBuNetwork);
        }

        public PartialViewResult PartialProfessionalNetworkList(string id)
        {
            List<Models.Membership> lBuNetwork = new List<Models.Membership>();
            try
            {
                var member = (from u in _db.Memberships
                              where u.MembershipNumber == id
                              select u).FirstOrDefault();

                if (member != null) // Mean address link of browse has Membership Number
                {
                    // Check if owner Member
                    if (member.Email == WebSecurity.CurrentUserName)
                    {
                        // Select list friend of current User
                        var lFriend = (from f in _db.Friends
                                       where f.UserId == f.FriendId && f.User.StatusId == 1 && f.User.Profile.CustomerTypeId == _db.LookupCultures.SingleOrDefault(x => x.LanguageId == 1 && x.Name.Equals("Professional")).LookupId
                                       select f).Take(10).ToList();

                        // Get profile of friend and add to list Friend
                        lBuNetwork = new List<Models.Membership>();
                        foreach (var item in lFriend)
                        {
                            var friend = (from f in _db.Memberships
                                          where f.UserId == item.FriendId && f.User.StatusId == 1
                                          select f).FirstOrDefault();

                            lBuNetwork.Add(friend);
                        }
                    }
                    else // If not owner
                    {
                        // Selet top 10 people has the same business Type
                        lBuNetwork = _db.Memberships.Where(w => w.BusinessAreaId == member.BusinessAreaId && w.User.Profile.CustomerTypeId == _db.LookupCultures.SingleOrDefault(x => x.LanguageId == 1 && x.Name.Equals("Professional")).LookupId && w.UserId != member.UserId).OrderBy(o => Guid.NewGuid()).Take(10).ToList();
                    } // End check owner Business Profile
                }
                else
                {
                    // End check NUll Select member with MemberShip Number
                    var current = _db.Memberships.Where(w => w.Email == WebSecurity.CurrentUserName).FirstOrDefault();
                    // Select list friend of current User
                    var lFriend = (from f in _db.Friends
                                   where f.UserId == current.UserId && f.StatusId == 9
                                   select f).Take(10).ToList();

                    // Get profile of friend and add to list Friend
                    lBuNetwork = new List<Models.Membership>();
                    foreach (var item in lFriend)
                    {
                        var friend = (from f in _db.Memberships
                                      where f.UserId == item.FriendId && f.User.StatusId == 1
                                      select f).FirstOrDefault();

                        lBuNetwork.Add(friend);
                    }
                } // Else is sure Owner
            }
            catch (Exception)
            {
                TempData["ParBuWarning"] = "Can not load list business";
            }
            return PartialView("_PartialProfessionalNetworkList", lBuNetwork);
        }

        public PartialViewResult TermsCondition()
        {
            string strUrl = "terms-and-conditions";

            PageCulture pageC = _db.PageCultures.SingleOrDefault(c => c.Url == strUrl);
            ViewBag.PageCountent = pageC;
            return PartialView();
        }

        /* Nguyen Thai Lam - Business Profile - Settings */
        [Authorize]
        public ActionResult Membership()
        {
            if (CheckSignUpRequired() == false)
            {
                return RedirectToAction("FProfile", "Account");
            }
            string strUrl = "membership";
            MultipleLanguage _Language = _db.MultipleLanguages.SingleOrDefault(m => m.Page == "PageContent" && m.Name == strUrl);
            LanguageModel _LanguageModel = new LanguageModel(_Language);
            ViewBag.PageContent = _LanguageModel;

            MultipleLanguage _LanguageTitle = _db.MultipleLanguages.SingleOrDefault(m => m.Page == "PageContent" && m.Name == (strUrl + "-title"));
            LanguageModel _LanguageModelTitle = new LanguageModel(_LanguageTitle);
            ViewBag.PageTitle = _LanguageModelTitle;

            MultipleLanguage _LanguageTitlePage = _db.MultipleLanguages.SingleOrDefault(m => m.Page == "PageContent" && m.Name == (strUrl + "-titlepage"));
            LanguageModel _LanguageModelTitlePage = new LanguageModel(_LanguageTitlePage);
            ViewBag.PageTitlePage = _LanguageModelTitlePage;

            //PageCulture pageC = _db.PageCultures.Where(c => c.Url == "membership").FirstOrDefault();
            return View();
        }

        /// <summary>
        /// This function check result payment for GoldMember and update ID gold member for membership
        /// </summary>
        /// <param name="AccessCode">The access code response from the API</param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult GComplete(string AccessCode)
        {
            if (AccessCode != "")
            {
                GetAccessCodeResultResponse result = helpers.GetPaymentResult(AccessCode);
                if (result != null)
                {
                    if (result.ResponseMessage == "A2000" || result.ResponseMessage == "A2008" || result.ResponseMessage == "A2010" || result.ResponseMessage == "A2011" || result.ResponseMessage == "A2016")
                    {
                        // Code upgrade member to Gold Member
                        var account = (from m in _db.Memberships
                                       where m.Email == User.Identity.Name
                                       select m).FirstOrDefault();

                        account.MembershipTypeId = 7;

                        // 6 months for gold member
                        if (result.TotalAmount >= 360 && result.TotalAmount < 720)
                        {
                            account.Gold_Expiry_Date = DateTime.Now.AddMonths(6);
                        } // 1 year for gold member
                        else if (result.TotalAmount >= 720)
                        {
                            account.Gold_Expiry_Date = DateTime.Now.AddYears(1);
                        }

                        _db.SaveChanges();

                        decimal MD_totalAmount = Convert.ToDecimal(result.TotalAmount);
                        PaymentsHistory pmt = new PaymentsHistory();
                        pmt.Created = DateTime.Now;
                        pmt.ReferencesId = (Int32)account.UserId;
                        pmt.StatusId = true;
                        pmt.TypePayment = 78;
                        pmt.UserId = account.UserId;
                        pmt.Amount = MD_totalAmount;

                        _db.PaymentsHistories.Add(pmt);
                        _db.SaveChanges();

                        PaymentsHistory pmtupdate = _db.PaymentsHistories.Single(p => p.Id == pmt.Id);
                        long plusId = 10000 + pmtupdate.Id;
                        var countryCode = (from c in _db.Countries where c.Id == pmtupdate.User.Membership.LocationId select c.CountryCode).FirstOrDefault().ToString();
                        string paymentnumber = countryCode + plusId.ToString().PadLeft(8, '0') + "GM"; //GM = gold member - code = 10 digits: countryCode + BussinessAreaID + 000000 + UserId

                        pmtupdate.PaymentNumber = paymentnumber;

                        _db.SaveChanges();
                    }
                    return View(result);
                }
                else
                {
                    return RedirectToAction("Error", "Home");
                }
            }
            else
            {
                return RedirectToAction("Error", "Home");
            }
        }

        public ActionResult GCancel()
        {
            return View();
        }

        //RESET PASSWORD
        [HttpPost]
        public ActionResult resetPassword(string email)
        {
            User user = new User();
            try
            {
                user = _db.Users.Single(o => o.Email == email);
            }
            catch (Exception e)
            {
                TempData["warning"] = "Email is NOT correct.";
                return RedirectToAction("Login", "Account");
            }
            try
            {

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
                _db.SaveChanges();
                SendEmailResetPassword(user.Profile.FirstName, newPass.Trim(), user.Email);
            }
            catch (Exception e)
            {
                TempData["warning"] = "There is exception in our system.";
                return RedirectToAction("Login", "Account");
            }
            //Tra ve ham success cua client de xu li
            TempData["success"] = "Your password was changed. We send new password to your email. Please check your email!";
            return RedirectToAction("Login", "Account");
        }

        /// <summary>
        /// Add events
        /// </summary>
        /// <param name="eventValue">Events text</param>
        /// <param name="publishType">Publist type</param>
        /// <returns></returns>
        [HttpPost]
        public PartialViewResult AddEvents(string eventValue, string publishType)
        {
            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            // New Wall information to save to DB
            Wall wallEnt = new Wall();
            wallEnt.UserWall = selectUser.Id;
            wallEnt.UserPost = selectUser.Id;
            wallEnt.WallTypeId = WALLTYPE_EVENTS;
            wallEnt.WallPublishTypeId = Convert.ToInt32(publishType);
            wallEnt.Events = eventValue;
            wallEnt.Created = DateTime.Now;

            // Add to Wall table
            _db.Walls.Add(wallEnt);
            _db.SaveChanges();

            // List Event - VuongVT: 06/28/2014  
            List<EventCommentModel> lstEventCommentModel = new List<EventCommentModel>();

            // Get list events to load list event after add an event
            var listEvents = (from wall in _db.Walls
                              where wall.UserWall == selectUser.Id
                                    && wall.Events != null
                              select wall).OrderByDescending(wall => wall.Id).ToList();

            foreach (var item in listEvents)
            {
                EventCommentModel eventCommentModel = new EventCommentModel();
                eventCommentModel.Event = item;

                var listCommentEvents = (from comment in _db.WallComments
                                         where comment.WallId == item.Id
                                               && comment.IsDeleted == false
                                         select comment).OrderBy(comment => comment.Id).ToList();

                eventCommentModel.ListComments = listCommentEvents;

                lstEventCommentModel.Add(eventCommentModel);
            }

            return PartialView("_EventPostPartial", lstEventCommentModel);
        }

        public string SendEmailToBusiness(string _fromName, string _businessName, string phoneNo, string country,string _toMail, string _fromMail, string _content, string _subject)
        {
            try
            {
                _accountService.UserSendMailToBusiness(_fromName, _businessName, _fromMail, _toMail, _subject, _content, phoneNo, country);
                return "Sucess";
            }
            catch
            {
                return "Fail";
            }
        }

        /// <summary>
        /// Add events
        /// </summary>
        /// <param name="eventValue">Events text</param>
        /// <param name="publishType">Publist type</param>
        /// <returns></returns>
        [HttpPost]
        public PartialViewResult AddNotes(string noteValue, string publishType)
        {
            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            // New Wall information to save to DB
            Wall wallEnt = new Wall();
            wallEnt.UserWall = selectUser.Id;
            wallEnt.UserPost = selectUser.Id;
            wallEnt.WallTypeId = WALLTYPE_NOTES;
            wallEnt.WallPublishTypeId = Convert.ToInt32(publishType);
            wallEnt.Notes = noteValue;
            wallEnt.Created = DateTime.Now;

            // Add to Wall table
            _db.Walls.Add(wallEnt);
            _db.SaveChanges();

            List<NoteCommentModel> lstNoteCommentModel = new List<NoteCommentModel>();

            // Get list events to load list event after add an event
            var listNotes = (from wall in _db.Walls
                             where wall.UserWall == selectUser.Id
                                   && wall.Notes != null
                             select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var note in listNotes)
            {
                NoteCommentModel noteCommentModel = new NoteCommentModel();
                noteCommentModel.Note = note;
                var listCommentNote = (from comment in _db.WallComments
                                       where comment.WallId == note.Id
                                             && comment.IsDeleted == false
                                       select comment).OrderBy(comment => comment.Id).ToList();
                noteCommentModel.ListCommentNotes = listCommentNote;
                lstNoteCommentModel.Add(noteCommentModel);
            }

            return PartialView("_NotePostPartial", lstNoteCommentModel);
        }

        [HttpPost]
        public PartialViewResult AddPhoto(FormCollection data)
        {
            string publishTypeValue = data["publishTypeValue"];
            var file = Request.Files["files"];
            var fileName = DateTime.Now.ToString("yyyyMMddmmss") + "_" + file.FileName;
            string path = Path.Combine(System.Web.HttpContext.Current.Server.MapPath("~/Images/WallPhotoPost/"), Path.GetFileName(fileName)); // Create path to save photo
            file.SaveAs(path); // Save image to sever

            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            // New Wall information to save to DB
            Wall wallEnt = new Wall();
            wallEnt.UserWall = selectUser.Id;
            wallEnt.UserPost = selectUser.Id;
            wallEnt.WallTypeId = WALLTYPE_PHOTO;
            wallEnt.WallPublishTypeId = Convert.ToInt32(publishTypeValue);
            wallEnt.Photo = fileName;
            wallEnt.Created = DateTime.Now;

            // Add to Wall table
            _db.Walls.Add(wallEnt);
            _db.SaveChanges();

            List<PhotoCommentModel> lstPhotoCommentModel = new List<PhotoCommentModel>();
            // Get list photos to load list photos after add a photo
            var listPhotos = (from wall in _db.Walls
                              where wall.UserWall == selectUser.Id
                                    && wall.Photo != null
                              select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var photo in listPhotos)
            {
                PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                photoCommentModel.Photo = photo;
                var listCommentPhoto = (from comment in _db.WallComments
                                        where comment.WallId == photo.Id
                                              && comment.IsDeleted == false
                                        select comment).OrderBy(comment => comment.Id).ToList();
                photoCommentModel.ListCommentPhotos = listCommentPhoto;
                lstPhotoCommentModel.Add(photoCommentModel);
            }

            return PartialView("_PhotoPostPartial", lstPhotoCommentModel);
        }

        public PartialViewResult AddCommentEvent(string wallId, string userPost, string commentContent)
        {
            int wallIdValue = Convert.ToInt32(wallId);
            int userPostValue = Convert.ToInt32(userPost);
            WallComment commentEnt = new WallComment();
            commentEnt.WallId = wallIdValue;
            commentEnt.UserPost = userPostValue;
            commentEnt.CommentContent = commentContent;
            commentEnt.Created = DateTime.Now;
            _db.WallComments.Add(commentEnt);
            _db.SaveChanges();

            EventCommentModel eventCommentModel = new EventCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            wallEnt.UserPost = userPostValue;
            eventCommentModel.Event = wallEnt;

            var listCommentEvents = (from comment in _db.WallComments
                                     where comment.WallId == wallIdValue
                                           && comment.IsDeleted == false
                                     select comment).OrderBy(comment => comment.Id).ToList();
            eventCommentModel.ListComments = listCommentEvents;

            return PartialView("_CommentEvent", eventCommentModel);
        }

        public PartialViewResult AddCommentNote(string wallId, string userPost, string commentContent)
        {
            int wallIdValue = Convert.ToInt32(wallId);
            int userPostValue = Convert.ToInt32(userPost);
            WallComment commentEnt = new WallComment();
            commentEnt.WallId = wallIdValue;
            commentEnt.UserPost = userPostValue;
            commentEnt.CommentContent = commentContent;
            commentEnt.Created = DateTime.Now;
            _db.WallComments.Add(commentEnt);
            _db.SaveChanges();

            NoteCommentModel noteCommentModel = new NoteCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            wallEnt.UserPost = userPostValue;
            noteCommentModel.Note = wallEnt;

            var listCommentNotes = (from comment in _db.WallComments
                                    where comment.WallId == wallIdValue
                                          && comment.IsDeleted == false
                                    select comment).OrderBy(comment => comment.Id).ToList();
            noteCommentModel.ListCommentNotes = listCommentNotes;

            return PartialView("_CommentNote", noteCommentModel);
        }

        public PartialViewResult AddCommentPhoto(string wallId, string userPost, string commentContent)
        {
            int wallIdValue = Convert.ToInt32(wallId);
            int userPostValue = Convert.ToInt32(userPost);
            WallComment commentEnt = new WallComment();
            commentEnt.WallId = wallIdValue;
            commentEnt.UserPost = userPostValue;
            commentEnt.CommentContent = commentContent;
            commentEnt.Created = DateTime.Now;
            _db.WallComments.Add(commentEnt);
            _db.SaveChanges();

            PhotoCommentModel photoCommentModel = new PhotoCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            wallEnt.UserPost = userPostValue;
            photoCommentModel.Photo = wallEnt;

            var listCommentPhotos = (from comment in _db.WallComments
                                     where comment.WallId == wallIdValue
                                           && comment.IsDeleted == false
                                     select comment).OrderBy(comment => comment.Id).ToList();
            photoCommentModel.ListCommentPhotos = listCommentPhotos;

            return PartialView("_CommentPhoto", photoCommentModel);
        }

        public PartialViewResult LoadAllCommentEvent(string wallId)
        {
            int wallIdValue = Convert.ToInt32(wallId);
            EventCommentModel eventCommentModel = new EventCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            eventCommentModel.Event = wallEnt;
            var listCommentNotes = (from comment in _db.WallComments
                                    where comment.WallId == wallIdValue
                                          && comment.IsDeleted == false
                                    select comment).OrderBy(comment => comment.Id).ToList();
            eventCommentModel.ListComments = listCommentNotes;
            ViewData["IsLoadAllCommentEvent"] = true;
            return PartialView("_CommentEvent", eventCommentModel);
        }

        public PartialViewResult LoadAllCommentNote(string wallId)
        {
            int wallIdValue = Convert.ToInt32(wallId);
            NoteCommentModel noteCommentModel = new NoteCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            noteCommentModel.Note = wallEnt;
            var listCommentNotes = (from comment in _db.WallComments
                                    where comment.WallId == wallIdValue
                                          && comment.IsDeleted == false
                                    select comment).OrderBy(comment => comment.Id).ToList();
            noteCommentModel.ListCommentNotes = listCommentNotes;
            ViewData["IsLoadAllCommentNote"] = true;
            return PartialView("_CommentNote", noteCommentModel);
        }

        public PartialViewResult LoadAllCommentPhoto(string wallId)
        {
            int wallIdValue = Convert.ToInt32(wallId);
            PhotoCommentModel photoCommentModel = new PhotoCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            photoCommentModel.Photo = wallEnt;
            var listCommentPhotos = (from comment in _db.WallComments
                                     where comment.WallId == wallIdValue
                                           && comment.IsDeleted == false
                                     select comment).OrderBy(comment => comment.Id).ToList();
            photoCommentModel.ListCommentPhotos = listCommentPhotos;
            ViewData["IsLoadAllCommentPhoto"] = true;
            return PartialView("_CommentPhoto", photoCommentModel);
        }

        public PartialViewResult DeleteCommentEvent(string wallId, string commentId)
        {
            // Delete comment of a event
            if (!string.IsNullOrEmpty(commentId))
            {
                var commentIdValue = Convert.ToInt32(commentId);
                var commentModel = (from comment in _db.WallComments
                                    where comment.Id == commentIdValue
                                    select comment).FirstOrDefault();
                commentModel.IsDeleted = true;
                _db.SaveChanges();
            }

            // Reload comment of event
            int wallIdValue = Convert.ToInt32(wallId);
            EventCommentModel eventCommentModel = new EventCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            eventCommentModel.Event = wallEnt;
            var listCommentNotes = (from comment in _db.WallComments
                                    where comment.WallId == wallIdValue
                                          && comment.IsDeleted == false
                                    select comment).OrderBy(comment => comment.Id).ToList();
            eventCommentModel.ListComments = listCommentNotes;
            ViewData["IsLoadAllCommentEvent"] = true;
            return PartialView("_CommentEvent", eventCommentModel);
        }

        public PartialViewResult DeleteCommentNote(string wallId, string commentId)
        {
            // Delete comment of a note
            if (!string.IsNullOrEmpty(commentId))
            {
                var commentIdValue = Convert.ToInt32(commentId);
                var commentModel = (from comment in _db.WallComments
                                    where comment.Id == commentIdValue
                                    select comment).FirstOrDefault();
                commentModel.IsDeleted = true;
                _db.SaveChanges();
            }

            int wallIdValue = Convert.ToInt32(wallId);
            NoteCommentModel noteCommentModel = new NoteCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            noteCommentModel.Note = wallEnt;
            var listCommentNotes = (from comment in _db.WallComments
                                    where comment.WallId == wallIdValue
                                          && comment.IsDeleted == false
                                    select comment).OrderBy(comment => comment.Id).ToList();
            noteCommentModel.ListCommentNotes = listCommentNotes;
            ViewData["IsLoadAllCommentNote"] = true;
            return PartialView("_CommentNote", noteCommentModel);
        }

        public PartialViewResult DeleteCommentPhoto(string wallId, string commentId)
        {
            // Delete comment of a note
            if (!string.IsNullOrEmpty(commentId))
            {
                var commentIdValue = Convert.ToInt32(commentId);
                var commentModel = (from comment in _db.WallComments
                                    where comment.Id == commentIdValue
                                    select comment).FirstOrDefault();
                commentModel.IsDeleted = true;
                _db.SaveChanges();
            }

            int wallIdValue = Convert.ToInt32(wallId);
            PhotoCommentModel photoCommentModel = new PhotoCommentModel();
            Wall wallEnt = new Wall();
            wallEnt.Id = wallIdValue;
            photoCommentModel.Photo = wallEnt;
            var listCommentPhotos = (from comment in _db.WallComments
                                     where comment.WallId == wallIdValue
                                           && comment.IsDeleted == false
                                     select comment).OrderBy(comment => comment.Id).ToList();
            photoCommentModel.ListCommentPhotos = listCommentPhotos;
            ViewData["IsLoadAllCommentPhoto"] = true;
            return PartialView("_CommentPhoto", photoCommentModel);
        }

        public PartialViewResult DeleteNotePost(string wallId)
        {
            // Delete a post
            if (!string.IsNullOrEmpty(wallId))
            {
                var wallIdValue = Convert.ToInt32(wallId);
                var wallEnt = (from wall in _db.Walls
                               where wall.Id == wallIdValue
                               select wall).FirstOrDefault();
                _db.Walls.Remove(wallEnt);

                // Delete all comments of this post
                var listComment = from wallComment in _db.WallComments
                                  where wallComment.WallId == wallIdValue
                                  select wallComment;
                foreach (var comment in listComment)
                {
                    _db.WallComments.Remove(comment);
                }

                _db.SaveChanges();
            }

            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            List<NoteCommentModel> lstNoteCommentModel = new List<NoteCommentModel>();

            // Get list events to load list event after add an event
            var listNotes = (from wall in _db.Walls
                             where wall.UserWall == selectUser.Id
                                   && wall.Notes != null
                             select wall).OrderByDescending(wall => wall.Id).ToList();

            foreach (var note in listNotes)
            {
                NoteCommentModel noteCommentModel = new NoteCommentModel();
                noteCommentModel.Note = note;
                var listCommentNote = (from comment in _db.WallComments
                                       where comment.WallId == note.Id
                                             && comment.IsDeleted == false
                                       select comment).OrderBy(comment => comment.Id).ToList();
                noteCommentModel.ListCommentNotes = listCommentNote;
                lstNoteCommentModel.Add(noteCommentModel);
            }

            return PartialView("_NotePostPartial", lstNoteCommentModel);
        }

        public PartialViewResult DeleteEventPost(string wallId)
        {
            // Delete a post
            if (!string.IsNullOrEmpty(wallId))
            {
                var wallIdValue = Convert.ToInt32(wallId);
                var wallEnt = (from wall in _db.Walls
                               where wall.Id == wallIdValue
                               select wall).FirstOrDefault();
                _db.Walls.Remove(wallEnt);

                // Delete all comments of this post
                var listComment = from wallComment in _db.WallComments
                                  where wallComment.WallId == wallIdValue
                                  select wallComment;
                foreach (var comment in listComment)
                {
                    _db.WallComments.Remove(comment);
                }

                _db.SaveChanges();
            }

            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            // List Event - VuongVT: 06/28/2014  
            List<EventCommentModel> lstEventCommentModel = new List<EventCommentModel>();

            // Get list events to load list event after add an event
            var listEvents = (from wall in _db.Walls
                              where wall.UserWall == selectUser.Id
                                    && wall.Events != null
                              select wall).OrderByDescending(wall => wall.Id).ToList();

            foreach (var item in listEvents)
            {
                EventCommentModel eventCommentModel = new EventCommentModel();
                eventCommentModel.Event = item;

                var listCommentEvents = (from comment in _db.WallComments
                                         where comment.WallId == item.Id
                                               && comment.IsDeleted == false
                                         select comment).OrderBy(comment => comment.Id).ToList();

                eventCommentModel.ListComments = listCommentEvents;

                lstEventCommentModel.Add(eventCommentModel);
            }

            return PartialView("_EventPostPartial", lstEventCommentModel);
        }

        public PartialViewResult DeletePhotoPost(string wallId)
        {
            // Delete a post
            if (!string.IsNullOrEmpty(wallId))
            {
                var wallIdValue = Convert.ToInt32(wallId);
                var wallEnt = (from wall in _db.Walls
                               where wall.Id == wallIdValue
                               select wall).FirstOrDefault();
                _db.Walls.Remove(wallEnt);

                // Delete all comments of this post
                var listComment = from wallComment in _db.WallComments
                                  where wallComment.WallId == wallIdValue
                                  select wallComment;
                foreach (var comment in listComment)
                {
                    _db.WallComments.Remove(comment);
                }

                string path = Path.Combine(System.Web.HttpContext.Current.Server.MapPath("~/Images/WallPhotoPost/"), Path.GetFileName(wallEnt.Photo)); // Create path to save photo
                System.IO.File.Delete(path);

                _db.SaveChanges();
            }

            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            List<PhotoCommentModel> lstPhotoCommentModel = new List<PhotoCommentModel>();

            // Get list photos to load list photos after add a photo
            var listPhotos = (from wall in _db.Walls
                              where wall.UserWall == selectUser.Id
                                    && wall.Photo != null
                              select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var photo in listPhotos)
            {
                PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                photoCommentModel.Photo = photo;
                var listCommentPhoto = (from comment in _db.WallComments
                                        where comment.WallId == photo.Id
                                              && comment.IsDeleted == false
                                        select comment).OrderBy(comment => comment.Id).ToList();
                photoCommentModel.ListCommentPhotos = listCommentPhoto;
                lstPhotoCommentModel.Add(photoCommentModel);
            }

            return PartialView("_PhotoPostPartial", lstPhotoCommentModel);
        }

        public PartialViewResult LoadAllNotePost()
        {
            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            List<NoteCommentModel> lstNoteCommentModel = new List<NoteCommentModel>();

            // Get list events to load list event after add an event
            var listNotes = (from wall in _db.Walls
                             where wall.UserWall == selectUser.Id
                                   && wall.Notes != null
                             select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var note in listNotes)
            {
                NoteCommentModel noteCommentModel = new NoteCommentModel();
                noteCommentModel.Note = note;
                var listCommentNote = (from comment in _db.WallComments
                                       where comment.WallId == note.Id
                                             && comment.IsDeleted == false
                                       select comment).OrderBy(comment => comment.Id).ToList();
                noteCommentModel.ListCommentNotes = listCommentNote;
                lstNoteCommentModel.Add(noteCommentModel);
            }
            ViewData["IsLoadAllNotePost"] = true;
            return PartialView("_NotePostPartial", lstNoteCommentModel);
        }

        public PartialViewResult LoadAllEventPost()
        {
            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            List<EventCommentModel> lstEventCommentModel = new List<EventCommentModel>();

            // Get list events to load list event after add an event
            var listNotes = (from wall in _db.Walls
                             where wall.UserWall == selectUser.Id
                                   && wall.Events != null
                             select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var note in listNotes)
            {
                EventCommentModel eventCommentModel = new EventCommentModel();
                eventCommentModel.Event = note;
                var listCommentNote = (from comment in _db.WallComments
                                       where comment.WallId == note.Id
                                             && comment.IsDeleted == false
                                       select comment).OrderBy(comment => comment.Id).ToList();
                eventCommentModel.ListComments = listCommentNote;
                lstEventCommentModel.Add(eventCommentModel);
            }
            ViewData["IsLoadAllEventPost"] = true;
            return PartialView("_EventPostPartial", lstEventCommentModel);
        }

        public PartialViewResult LoadAllPhotoPost()
        {
            // Get current user logged information
            var selectUser = (from user in _db.Users
                              where user.Email == WebSecurity.CurrentUserName
                              select user).FirstOrDefault();

            List<PhotoCommentModel> lstPhotoCommentModel = new List<PhotoCommentModel>();

            // Get list photos to load list photos after add an photo
            var listPhotos = (from wall in _db.Walls
                              where wall.UserWall == selectUser.Id
                                    && wall.Photo != null
                              select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var photo in listPhotos)
            {
                PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                photoCommentModel.Photo = photo;
                var listCommentPhoto = (from comment in _db.WallComments
                                        where comment.WallId == photo.Id
                                              && comment.IsDeleted == false
                                        select comment).OrderBy(comment => comment.Id).ToList();
                photoCommentModel.ListCommentPhotos = listCommentPhoto;
                lstPhotoCommentModel.Add(photoCommentModel);
            }
            ViewData["IsLoadAllPhotoPost"] = true;
            return PartialView("_PhotoPostPartial", lstPhotoCommentModel);
        }

        public PartialViewResult AddNotesMemberWall(string noteValue, string publishType, string memberNum)
        {
            // Get user's wall information
            var infoWallUser = (from membershipEnt in _db.Memberships
                                where membershipEnt.MembershipNumber == memberNum
                                select membershipEnt).FirstOrDefault();

            var userPostInfo = (from user in _db.Users
                                where user.Email == WebSecurity.CurrentUserName
                                select user).FirstOrDefault();

            // New Wall information to save to DB
            Wall wallEnt = new Wall();
            wallEnt.UserWall = infoWallUser.UserId;
            wallEnt.UserPost = userPostInfo.Id;
            wallEnt.WallTypeId = WALLTYPE_EVENTS;
            wallEnt.WallPublishTypeId = Convert.ToInt32(publishType);
            wallEnt.Notes = noteValue;
            wallEnt.Created = DateTime.Now;

            // Add to Wall table
            _db.Walls.Add(wallEnt);
            _db.SaveChanges();

            List<NoteCommentModel> lstNoteCommentModel = new List<NoteCommentModel>();

            // Get list note to load list note after add an note
            var listNotes = (from wall in _db.Walls
                             where wall.UserWall == infoWallUser.UserId
                                   && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                   && wall.Notes != null
                             select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var note in listNotes)
            {
                NoteCommentModel noteCommentModel = new NoteCommentModel();
                noteCommentModel.Note = note;
                var listCommentNote = (from comment in _db.WallComments
                                       where comment.WallId == note.Id
                                             && comment.IsDeleted == false
                                       select comment).OrderBy(comment => comment.Id).ToList();
                noteCommentModel.ListCommentNotes = listCommentNote;
                lstNoteCommentModel.Add(noteCommentModel);
            }

            return PartialView("_NotePostPartial", lstNoteCommentModel);
        }

        public PartialViewResult AddEventsMemberWall(string eventValue, string publishType, string memberNum)
        {
            // Get user's wall information
            var infoWallUser = (from membershipEnt in _db.Memberships
                                where membershipEnt.MembershipNumber == memberNum
                                select membershipEnt).FirstOrDefault();

            var userPostInfo = (from user in _db.Users
                                where user.Email == WebSecurity.CurrentUserName
                                select user).FirstOrDefault();

            // New Wall information to save to DB
            Wall wallEnt = new Wall();
            wallEnt.UserWall = infoWallUser.UserId;
            wallEnt.UserPost = userPostInfo.Id;
            wallEnt.WallTypeId = WALLTYPE_EVENTS;
            wallEnt.WallPublishTypeId = Convert.ToInt32(publishType);
            wallEnt.Events = eventValue;
            wallEnt.Created = DateTime.Now;

            // Add to Wall table
            _db.Walls.Add(wallEnt);
            _db.SaveChanges();

            // List Event - VuongVT: 06/28/2014  
            List<EventCommentModel> lstEventCommentModel = new List<EventCommentModel>();

            // Get list events to load list event after add an event
            var listEvents = (from wall in _db.Walls
                              where wall.UserWall == infoWallUser.UserId
                                    && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                    && wall.Events != null
                              select wall).OrderByDescending(wall => wall.Id).ToList();

            foreach (var item in listEvents)
            {
                EventCommentModel eventCommentModel = new EventCommentModel();
                eventCommentModel.Event = item;

                var listCommentEvents = (from comment in _db.WallComments
                                         where comment.WallId == item.Id
                                               && comment.IsDeleted == false
                                         select comment).OrderBy(comment => comment.Id).ToList();

                eventCommentModel.ListComments = listCommentEvents;

                lstEventCommentModel.Add(eventCommentModel);
            }

            return PartialView("_EventPostPartial", lstEventCommentModel);
        }

        [HttpPost]
        public PartialViewResult AddPhotoMemberWall(FormCollection data)
        {
            string publishTypeValue = data["publishTypeValue"];
            var file = Request.Files["files"];
            var memberNum = data["memberNum"];
            var fileName = DateTime.Now.ToString("yyyyMMddmmss") + "_" + file.FileName;
            string path = Path.Combine(System.Web.HttpContext.Current.Server.MapPath("~/Images/WallPhotoPost/"), Path.GetFileName(fileName)); // Create path to save photo
            file.SaveAs(path); // Save image to sever

            // Get user's wall information
            var infoWallUser = (from membershipEnt in _db.Memberships
                                where membershipEnt.MembershipNumber == memberNum
                                select membershipEnt).FirstOrDefault();

            // Get current user logged information
            var userPostInfo = (from user in _db.Users
                                where user.Email == WebSecurity.CurrentUserName
                                select user).FirstOrDefault();

            // New Wall information to save to DB
            Wall wallEnt = new Wall();
            wallEnt.UserWall = infoWallUser.UserId;
            wallEnt.UserPost = userPostInfo.Id;
            wallEnt.WallTypeId = WALLTYPE_PHOTO;
            wallEnt.WallPublishTypeId = Convert.ToInt32(publishTypeValue);
            wallEnt.Photo = fileName;
            wallEnt.Created = DateTime.Now;

            // Add to Wall table
            _db.Walls.Add(wallEnt);
            _db.SaveChanges();

            List<PhotoCommentModel> lstPhotoCommentModel = new List<PhotoCommentModel>();
            // Get list photos to load list photos after add a photo
            var listPhotos = (from wall in _db.Walls
                              where wall.UserWall == infoWallUser.UserId
                                    && wall.WallPublishTypeId == WALLPUBLISHTYPE_SHARE
                                    && wall.Photo != null
                              select wall).OrderByDescending(wall => wall.Id).ToList();
            foreach (var photo in listPhotos)
            {
                PhotoCommentModel photoCommentModel = new PhotoCommentModel();
                photoCommentModel.Photo = photo;
                var listCommentPhoto = (from comment in _db.WallComments
                                        where comment.WallId == photo.Id
                                              && comment.IsDeleted == false
                                        select comment).OrderBy(comment => comment.Id).ToList();
                photoCommentModel.ListCommentPhotos = listCommentPhoto;
                lstPhotoCommentModel.Add(photoCommentModel);
            }

            return PartialView("_PhotoPostPartial", lstPhotoCommentModel);
        }
        /* Nguyen Thai Lam - Business Profile - Settings */

        /* CHANGE COVER PHOTO BY CROPPIC LIBRABY */
        //[HttpPost]
        //public JsonResult UploadCover(HttpPostedFileBase img)
        //{
        //    var arrAllow = new string[] { ".jpg", ".png" };
        //    var photo = WebImage.GetImageFromRequest();

        //    if (arrAllow.Contains(img.FileName.Substring(img.FileName.LastIndexOf('.'))))
        //    {
        //        if (photo != null)
        //        {
        //            string strFileName = img.FileName;
        //            photo.Save(Path.Combine(strCoverPath, strFileName));
        //        }
        //    }

        //    Response response = new Response
        //    {
        //        status = "success",
        //        url = "/Images/Covers/" + img.FileName,
        //        width = photo.Width.ToString(),
        //        height = photo.Height.ToString()

        //    };
        //    return Json(response, JsonRequestBehavior.AllowGet);
        //}

        // UPLOAD COVER NOT CROPPIC
        [HttpPost]
        public JsonResult UploadPhotoJson(HttpPostedFileBase img)
        {
            var arrAllow = new string[] { ".jpg", ".png" };
            var photo = WebImage.GetImageFromRequest();

            if (photo != null)
            {
                if (arrAllow.Contains(img.FileName.Substring(img.FileName.LastIndexOf('.'))))
                {
                    if (photo != null)
                    {
                        string strFileName = img.FileName;
                        photo.Save(Path.Combine(strCoverPath, strFileName));
                    }
                }

                var response = new
                {
                    status = "success",
                    imageURL = "/Images/Covers/" + img.FileName,
                    imageName = img.FileName
                };
                return Json(response, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var response = new
                {
                    status = "error",
                    error = "image is null"
                };
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SavePhotoJson(string imageName)
        {
            // Get current Account logged
            if (WebSecurity.IsAuthenticated)
            {
                var obCurrent = (from m in _db.Memberships
                                 where m.Email == WebSecurity.CurrentUserName
                                 select m).FirstOrDefault();

                // Update Cover Photo
                obCurrent.CoverPhoto = imageName;
                _db.SaveChanges();

                // return 
                var response = new
                {
                    status = "success"
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var response = new
                {
                    status = "error"
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult CancelPhotoJson(string path)
        {
            // Get current Account logged
            if (WebSecurity.IsAuthenticated)
            {
                var obCurrent = (from m in _db.Memberships
                                 where m.Email == WebSecurity.CurrentUserName
                                 select m).FirstOrDefault();

                FileInfo image = new FileInfo(Server.MapPath(path));
                image.Delete();

                // return 
                var response = new
                {
                    status = "success",
                    oldImage = "/Images/Covers/" + obCurrent.CoverPhoto
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var response = new
                {
                    status = "error"
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }

        [Authorize]
        public ActionResult ProfessionalFirstSignUp()
        {
            //if (CheckSignUpRequired() == true)
            //{
            //    return RedirectToAction("ProfessionalProfile", "Account");
            //}
            if (CheckSignUpRequired() == true)
            {
                return RedirectToAction("ProfessionalProfile", "Account");
            }
            
            return View();
        }


        /*Added By Chi Nguyen*/
        [Authorize]
        [HttpPost]
        public ActionResult ProfessionalFirstSignUp(Models.Membership model, Models.MembershipProfessional modelprofessional, HttpPostedFileBase uploadLogo)
        {
            //get current user who is logging
            var user = memProvider.GetUserByEmail(User.Identity.Name);

            var acc_Membership = new Models.Membership();
            var acc_MembershipProfessional = new Models.MembershipProfessional();

            if (ModelState.IsValid)
            {
                acc_Membership.UserId = user.Id;
                acc_MembershipProfessional.UserId = user.Id;
                acc_MembershipProfessional.LanguageId = 1;

                if (model.ContactNumber != null)
                {
                    acc_Membership.ContactNumber = model.ContactNumber.Trim();
                }

                if (model.BusinessAreaId != 0)
                {
                    acc_Membership.BusinessAreaId = model.BusinessAreaId;
                }

                if (model.LocationId != 0)
                {
                    acc_Membership.LocationId = model.LocationId;
                }

                if (model.ProvinceId != 0)
                {
                    acc_Membership.ProvinceId = model.ProvinceId;
                }

                if (model.DistrictId != 0)
                {
                    acc_Membership.DistrictId = model.DistrictId;
                }

                if (model.PostalAddress != null)
                {
                    acc_Membership.PostalAddress = model.PostalAddress.Trim();
                }

                if (model.JobTitle != null)
                {
                    acc_Membership.JobTitle = model.JobTitle.Trim();
                    acc_MembershipProfessional.JobTitle = modelprofessional.JobTitle;
                }

                if (model.WebsiteAddress != null)
                {
                    acc_Membership.WebsiteAddress = model.WebsiteAddress.Trim();
                }

                if (model.Tel != null)
                {
                    acc_Membership.Tel = model.Tel.Trim();
                }

                if (modelprofessional.Mobile != null)
                {
                    acc_MembershipProfessional.Mobile = modelprofessional.Mobile;
                }

                if (modelprofessional.Education != null)
                {
                    acc_MembershipProfessional.Education = modelprofessional.Education;
                }

                if (modelprofessional.Experiences != null)
                {
                    acc_MembershipProfessional.Experiences = modelprofessional.Experiences;
                }

                if (modelprofessional.ComputerSkills != null)
                {
                    acc_MembershipProfessional.ComputerSkills = modelprofessional.ComputerSkills;
                }

                if (modelprofessional.Hobbies != null)
                {
                    acc_MembershipProfessional.Hobbies = modelprofessional.Hobbies;
                }

                /**** Static Value of Membership ****/
                acc_Membership.DateOfBirth = user.Profile.BirthDay;
                acc_Membership.Email = user.Email;
                acc_Membership.LastModified = DateTime.Now;
                acc_Membership.Created = DateTime.Now;
                acc_Membership.MembershipTypeId = 6; // 6: Free membership
                acc_Membership.RankingValue = 1; // After fill Required SignUp ranking value is 1
                acc_Membership.PageViewed = 0;
                acc_Membership.BalanceCreditPoint = Convert.ToDecimal(0);
                acc_Membership.IsDisplayContactNumber = true;
                acc_Membership.IsDisplayDateOfBirth = true;
                acc_Membership.IsDisplayPostalAddress = true;
                /**** Static Value of Membership ****/
                /***** Generate MemebershipCode****/
                string strMembershipNumber = "";
                var countryCode = (from c in _db.Countries where c.Id == acc_Membership.LocationId select c.CountryCode).FirstOrDefault().ToString();
                // MemberShip Number = 12 digits: 2 (Country Code) + 2 (Business AreaID) + Membership ID
                string strCodeId = acc_Membership.UserId.ToString().PadLeft(8, '0');

                strMembershipNumber = countryCode + acc_Membership.BusinessAreaId.ToString() + strCodeId;
                acc_Membership.MembershipNumber = strMembershipNumber;
                /***** Generate MemebershipCode****/
                // After set all field save to db
                _db.Memberships.Add(acc_Membership);
                //Store to MembershipProfessional
                _db.MembershipProfessionals.Add(acc_MembershipProfessional);
                _db.SaveChanges();

                // Upload 2 photo to server
                // Upload Logo photo if has
                /*** noted: Not yet check duplicate name of photo and delete old photo */
                if (uploadLogo != null && uploadLogo.ContentLength > 0)
                {
                    if ((uploadLogo.ContentLength / 1024 / 1024) <= 2) // size valid
                    {
                        // Get filename of uploadLogo
                        //var fileNameLogo = uploadLogo.FileName;
                        Guid strGuid = Guid.NewGuid();
                        string fileNameLogo = strGuid.ToString() + "." + uploadLogo.FileName.Split('.').LastOrDefault().ToString();
                        helpers.UploadPhoto(uploadLogo, fileNameLogo, config.strAvatarImagesPath);

                        // Save file name of image to database
                        acc_Membership.Logo = fileNameLogo;
                    }
                    else
                    {
                        TempData["ProfessionalProfileSizeWarning"] = status.strInvalidProfessionalFileSize;
                        return RedirectToAction("ProfessionalFirstSignUp", "Account");
                    }
                }

                /***** Save Membership Category *****/
                var obMemberCate = new MembershipCategory();
                obMemberCate.UserId = acc_Membership.UserId;
                obMemberCate.CategoryId = Convert.ToInt32(model.BusinessAreaId);
                _db.MembershipCategories.Add(obMemberCate);
                _db.SaveChanges();
                /***** Save Membership Category *****/

                /***** Create First Free MarketDisplay for FreeMember *****/
                TempData["ProfessionalProfileSuccess"] = status.strUpdateProfessionalProfileSuccess;
                return RedirectToAction("ProfessionalProfile", "Account");
            }
            else
            {
                TempData["FProfilewarning"] = status.strUpdateProfessionalProfileUnsuccess;
                return RedirectToAction("ProfessionalFirstSignUp", "Account");
            }
        }
        /*End Added By Chi Nguyen*/

        /* CHANGE COVER PHOTO BY CROPPIC LIBRABY */
        #endregion

        #region Helpers
        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
        }

        internal class ExternalLoginResult : ActionResult
        {
            public ExternalLoginResult(string provider, string returnUrl)
            {
                Provider = provider;
                ReturnUrl = returnUrl;
            }

            public string Provider { get; private set; }
            public string ReturnUrl { get; private set; }

            public override void ExecuteResult(ControllerContext context)
            {
                OAuthWebSecurity.RequestAuthentication(Provider, ReturnUrl);
            }
        }

        private static string ErrorCodeToString(MembershipCreateStatus createStatus)
        {
            // See http://go.microsoft.com/fwlink/?LinkID=177550 for
            // a full list of status codes.
            switch (createStatus)
            {
                case MembershipCreateStatus.DuplicateUserName:
                    return "User name already exists. Please enter a different user name.";

                case MembershipCreateStatus.DuplicateEmail:
                    return "A user name for that e-mail address already exists. Please enter a different e-mail address.";

                case MembershipCreateStatus.InvalidPassword:
                    return "The password provided is invalid. Please enter a valid password value.";

                case MembershipCreateStatus.InvalidEmail:
                    return "The e-mail address provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.ProviderError:
                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipCreateStatus.UserRejected:
                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                default:
                    return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }
        }
        #endregion

        #region Library
        public List<User> GetAllUser()
        {
            var users = (from u in _db.Users select u).OrderByDescending(x => x.Id).ToList();

            return users;
        }

        /// <summary>
        /// Function Create the ACCESS CODE for put to the Card Submit Form to payment API.
        /// </summary>
        /// <param name="id">the number amount of payment</param>
        /// <returns></returns>
        public JsonResult CreateAccessCode(int id)
        {
            if (id == 720 || id == 360)
            {
                // Get Account by Email
                var Account = new User();
                if (User.Identity.IsAuthenticated) // Check user must logged
                {
                    // Get User from database by the email of account
                    Account = memProvider.GetUserByEmail(User.Identity.Name);
                }
                var member = (from m in _db.Memberships where m.Email == Account.Email select m).FirstOrDefault();

                // Create Object request to create access code.
                CreateAccessCodeRequest request = new Models.CreateAccessCodeRequest();
                request.Customer.FirstName = Account.Profile.FirstName;
                request.Customer.LastName = Account.Profile.LastName;
                request.Customer.Email = member.Email;
                request.Customer.Country = _db.Countries.Where(w => w.Id == member.LocationId).FirstOrDefault().CountryCode.ToString();
                request.Customer.Phone = "";

                request.Items.Add(new Items { SKU = "GoldMember", Description = "Upgrade to GoldMember" });

                request.Payment.TotalAmount = id * 100;
                request.Payment.InvoiceDescription = "Upgrade Account to Gold Member";
                request.Payment.InvoiceReference = "";
                request.Payment.InvoiceNumber = "";

                request.RedirectUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/Account/GComplete";
                request.CancelUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/Account/Cancel";
                request.Method = (Method)1;
                request.TransactionType = "Purchase";
                //request.LogoUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/Images/logo.png";
                request.HeaderText = "E-Tradebook payment";
                request.CustomView = "bootstrap";

                // Call CreateAccessCode function
                return Json(helpers.CreateAccessCodeJson(request), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { Error = "Invalid payment" }, JsonRequestBehavior.AllowGet);
            }
        }

        private void SendEmailResetPassword(string cust_Name, string newPassword, string receive)
        {
            string email = ConfigurationManager.AppSettings["SystemEmail"].ToString();
            string pass = ConfigurationManager.AppSettings["SystemEmailPassword"].ToString();
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress("customerservice@e-tradebook.com");
            mail.To.Add(new MailAddress(receive));

            mail.Subject = "[e-TradeBook.com] Your Password Has Been Reset";
            mail.SubjectEncoding = System.Text.Encoding.UTF8;
            mail.IsBodyHtml = true;
            string bodyHTML = "<table cellspacing='0' cellpadding='0' border='0' style='background-color: #f0f7fc; border: 1px solid #a5cae4;'>";
            bodyHTML += "<tbody>";
            bodyHTML += "<tr>";
            bodyHTML += "<td style='background-color: #d7edfc; padding: 5px 10px; border-bottom: 1px solid #a5cae4; font-family: 'Trebuchet MS', Helvetica, Arial, sans-serif; font-size: 11px; line-height: 1.231;'>";
            bodyHTML += "<a rel='nofollow' style='color: #176093; text-decoration: none;'>e-TradeBook - Online Business Network</a>";
            bodyHTML += "</td>";
            bodyHTML += "</tr>";
            bodyHTML += "<tr>";
            bodyHTML += "<td style='background-color: #fcfcff; padding: 1em; color: #141414; font-family: 'Trebuchet MS', Helvetica, Arial, sans-serif; font-size: 13px; line-height: 1.231;'>";
            bodyHTML += "<strong>Dear " + cust_Name + ",</strong>";
            bodyHTML += "<p style='margin-top: 10px;'>Your new password is <b style='font-size:17px'>" + newPassword + "</b></p>";
            bodyHTML += "<p style='margin-top: 10px;'>Login to <a rel='nofollow' style='color: #176093; text-decoration: none;' href='" + Request.Url.GetLeftPart(UriPartial.Authority) + "/account/login'>http://e-tradebook.com</a></p>";
            bodyHTML += "<p style='margin-top: 10px;'>It is highly recommended that you set a new, more memorable password through the 'Change Password' link once you login.</p>";
            bodyHTML += "<p>Thanks,<br>e-TradeBook - Onine Business Network</p>";
            bodyHTML += "</td>";
            bodyHTML += "</tr>";
            bodyHTML += "</tbody></table>";

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

        public bool CheckSignUpRequired()
        {
            // Get Account by Email
            var Account = new User();

            if (User.Identity.IsAuthenticated) // Check user must logged
            {
                // Get User from database by the email of account
                Account = memProvider.GetUserByEmail(User.Identity.Name);
            }

            var member = (from m in _db.Memberships where m.UserId == Account.Id select m).FirstOrDefault();

            if (member == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void ResizeImage(string originalImagePath, string newImagePath, int width, int height)
        {
            Size newSize = new Size(width, height);

            using (Image originalImage = Image.FromFile(originalImagePath))
            {
                //Graphics objects can not be created from bitmaps with an Indexed Pixel Format, use RGB instead.
                PixelFormat format = originalImage.PixelFormat;
                if (format.ToString().Contains("Indexed"))
                    format = PixelFormat.Format24bppRgb;

                using (Bitmap newImage = new Bitmap(newSize.Width, newSize.Height, originalImage.PixelFormat))
                {
                    using (Graphics canvas = Graphics.FromImage(newImage))
                    {
                        canvas.SmoothingMode = SmoothingMode.AntiAlias;
                        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        canvas.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        canvas.DrawImage(originalImage, new Rectangle(new Point(0, 0), newSize));
                        newImage.Save(newImagePath, originalImage.RawFormat);
                    }
                }
            }
        }

        public int CountPhoto(long userId)
        {

            //display all photo, about : in market display, displayphoto, setting photo
            var lstMarketDisplay = (from marketDisplay in _db.MarketDisplays
                                    where marketDisplay.UserId == userId
                                    && marketDisplay.Photo != string.Empty
                                    select marketDisplay).ToList();
            var marketDisplayNum = lstMarketDisplay.Count();
            var marketDisplayPhotoNum = 0;
            foreach (var marketDisplay in lstMarketDisplay)
            {
                var marketDisplayPhoto = (from mdp in _db.MarketDisplayPhotos
                                          where mdp.MarketDisplayId == marketDisplay.Id
                                          && mdp.Image != string.Empty
                                          select mdp).FirstOrDefault();
                if (marketDisplayPhoto != null)
                {
                    marketDisplayPhotoNum = marketDisplayPhotoNum + 1;
                }
            }
            var settingPhotoNum = 0;
            settingPhotoNum = (from settingPhoto in _db.SettingPhotoes
                               where settingPhoto.UserId == userId
                                      && settingPhoto.Photo != string.Empty
                               select settingPhoto).Count();
            //return marketDisplayNum + marketDisplayPhotoNum + settingPhotoNum;
            return settingPhotoNum;
        }

        #endregion

        //
        //POST: Account/DeleteArticles
        public ActionResult DeleteArticles(FormCollection form)
        {
            try
            {
                var membersSelected = form["article"];
                if (membersSelected != null && membersSelected.Length > 0)
                {
                    var context = new ITechETradeBook_v2.Models.etradebook_Entities();
                    var part = membersSelected.Split(',');

                    //Insert selected members to Friend table
                    if (part != null && part.Count() > 0)
                    {
                        foreach (var item in part)
                        {
                            var friendId = Convert.ToInt32(item.Trim().ToString());
                            var friend = context.Memberships.Where(w => w.UserId == friendId).FirstOrDefault();
                            var objProfessionalArticle = context.ProfessionalArticles.Where(w => w.Id == friendId).FirstOrDefault();

                            context.ProfessionalArticles.Remove(objProfessionalArticle);
                            context.SaveChanges();
                        }
                    }
                }
                TempData["MessageBusinessSuccess"] = "You have been deleted the article successfully";
                return RedirectToAction("ProfessionalProfile");
            }
            catch (Exception)
            {
                TempData["MessageBusinessWarning"] = "You have been not deleted the article successfully";
                return RedirectToAction("Search");
            }
        }

        #region Rapid
        /*********** Default function of MVC 4 
        //
        // POST: /Account/Disassociate

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Disassociate(string provider, string providerUserId)
        {
            string ownerAccount = OAuthWebSecurity.GetUserName(provider, providerUserId);
            ManageMessageId? message = null;

            // Only disassociate the account if the currently logged in user is the owner
            if (ownerAccount == User.Identity.Name)
            {
                // Use a transaction to prevent the user from deleting their last login credential
                using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
                {
                    bool hasLocalAccount = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
                    if (hasLocalAccount || OAuthWebSecurity.GetAccountsFromUserName(User.Identity.Name).Count > 1)
                    {
                        OAuthWebSecurity.DeleteAccount(provider, providerUserId);
                        scope.Complete();
                        message = ManageMessageId.RemoveLoginSuccess;
                    }
                }
            }

            return RedirectToAction("Manage", new { Message = message });
        }

        //
        // GET: /Account/Manage
        [Authorize]
        public ActionResult Manage()
        {
            var user = memProvider.GetUserByEmail(User.Identity.Name.Trim());
            return View(user);
        }

        //
        // POST: /Account/Manage

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Manage(LocalPasswordModel model)
        {
            bool hasLocalAccount = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
            ViewBag.HasLocalPassword = hasLocalAccount;
            ViewBag.ReturnUrl = Url.Action("Manage");
            if (hasLocalAccount)
            {
                if (ModelState.IsValid)
                {
                    // ChangePassword will throw an exception rather than return false in certain failure scenarios.
                    bool changePasswordSucceeded;
                    try
                    {
                        changePasswordSucceeded = WebSecurity.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword);
                    }
                    catch (Exception)
                    {
                        changePasswordSucceeded = false;
                    }

                    if (changePasswordSucceeded)
                    {
                        return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
                    }
                    else
                    {
                        ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
                    }
                }
            }
            else
            {
                // User does not have a local password so remove any validation errors caused by a missing
                // OldPassword field
                ModelState state = ModelState["OldPassword"];
                if (state != null)
                {
                    state.Errors.Clear();
                }

                if (ModelState.IsValid)
                {
                    try
                    {
                        WebSecurity.CreateAccount(User.Identity.Name, model.NewPassword);
                        return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
                    }
                    catch (Exception e)
                    {
                        ModelState.AddModelError("", e);
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // POST: /Account/ExternalLogin

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            return new ExternalLoginResult(provider, Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl }));
        }

        //
        // GET: /Account/ExternalLoginCallback

        [AllowAnonymous]
        public ActionResult ExternalLoginCallback(string returnUrl)
        {
            AuthenticationResult result = OAuthWebSecurity.VerifyAuthentication(Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl }));
            if (!result.IsSuccessful)
            {
                return RedirectToAction("ExternalLoginFailure");
            }

            if (OAuthWebSecurity.Login(result.Provider, result.ProviderUserId, createPersistentCookie: false))
            {
                return RedirectToLocal(returnUrl);
            }

            if (User.Identity.IsAuthenticated)
            {
                // If the current user is logged in add the new account
                OAuthWebSecurity.CreateOrUpdateAccount(result.Provider, result.ProviderUserId, User.Identity.Name);
                return RedirectToLocal(returnUrl);
            }
            else
            {
                // User is new, ask for their desired membership name
                string loginData = OAuthWebSecurity.SerializeProviderUserId(result.Provider, result.ProviderUserId);
                ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData(result.Provider).DisplayName;
                ViewBag.ReturnUrl = returnUrl;
                return View("ExternalLoginConfirmation", new RegisterExternalLoginModel { UserName = result.UserName, ExternalLoginData = loginData });
            }
        }

        //
        // POST: /Account/ExternalLoginConfirmation

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLoginConfirmation(RegisterExternalLoginModel model, string returnUrl)
        {
            string provider = null;
            string providerUserId = null;

            if (User.Identity.IsAuthenticated || !OAuthWebSecurity.TryDeserializeProviderUserId(model.ExternalLoginData, out provider, out providerUserId))
            {
                return RedirectToAction("Manage");
            }

            if (ModelState.IsValid)
            {
                // Insert a new user into the database
                using (UsersContext db = new UsersContext())
                {
                    UserProfile user = db.UserProfiles.FirstOrDefault(u => u.UserName.ToLower() == model.UserName.ToLower());
                    // Check if user already exists
                    if (user == null)
                    {
                        // Insert name into the profile table
                        db.UserProfiles.Add(new UserProfile { UserName = model.UserName });
                        db.SaveChanges();

                        OAuthWebSecurity.CreateOrUpdateAccount(provider, providerUserId, model.UserName);
                        OAuthWebSecurity.Login(provider, providerUserId, createPersistentCookie: false);

                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        ModelState.AddModelError("UserName", "User name already exists. Please enter a different user name.");
                    }
                }
            }

            ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData(provider).DisplayName;
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        //
        // GET: /Account/ExternalLoginFailure

        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        [AllowAnonymous]
        [ChildActionOnly]
        public ActionResult ExternalLoginsList(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return PartialView("_ExternalLoginsListPartial", OAuthWebSecurity.RegisteredClientData);
        }

        [ChildActionOnly]
        public ActionResult RemoveExternalLogins()
        {
            ICollection<OAuthAccount> accounts = OAuthWebSecurity.GetAccountsFromUserName(User.Identity.Name);
            List<ExternalLogin> externalLogins = new List<ExternalLogin>();
            foreach (OAuthAccount account in accounts)
            {
                AuthenticationClientData clientData = OAuthWebSecurity.GetOAuthClientData(account.Provider);

                externalLogins.Add(new ExternalLogin
                {
                    Provider = account.Provider,
                    ProviderDisplayName = clientData.DisplayName,
                    ProviderUserId = account.ProviderUserId,
                });
            }

            ViewBag.ShowRemoveButton = externalLogins.Count > 1 || OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
            return PartialView("_RemoveExternalLoginsPartial", externalLogins);
        }

        *****************************/
        #endregion
    }
}
