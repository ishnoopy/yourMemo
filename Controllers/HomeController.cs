using Firebase.Auth;
using FireSharp.Interfaces;
using FireSharp.Response;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using yourMemo.Models;
using FirebaseConfig = FireSharp.Config.FirebaseConfig;

namespace yourMemo.Controllers
{
    public class HomeController : Controller
    {

        private readonly IWebHostEnvironment _webHostEnvironment; // allows you to access the different folders in your application.



        FirebaseAuthProvider auth;

        public HomeController(IWebHostEnvironment webHostEnvironment)
        {
            auth = new FirebaseAuthProvider(
                            new Firebase.Auth.FirebaseConfig("AIzaSyBpDM45w_uF6T_spPjT-526pancb-Nz3So"));
            _webHostEnvironment = webHostEnvironment;
        }



        public IActionResult Index()
        {
            var token = HttpContext.Session.GetString("_UserToken");
            if (token != null)
            {
                ViewBag.sessionVerify = token;
                return View();
            }
            else
            {
                return View();
            }
        }

        public IActionResult Registration()
        {
            return View();
        }

        //registration process
        [HttpPost]
        public async Task<IActionResult> Registration(LogInModel loginModel)
        {
            try
            {
                //create the user
                await auth.CreateUserWithEmailAndPasswordAsync(loginModel.Email, loginModel.Password);
                //log in the new user
                var fbAuthLink = await auth
                                .SignInWithEmailAndPasswordAsync(loginModel.Email, loginModel.Password);
                string token = fbAuthLink.FirebaseToken;
                //saving the token in a session variable
                if (token != null)
                {
                    HttpContext.Session.SetString("_UserToken", token);

                    return RedirectToAction("Index");
                }
            }
            catch (FirebaseAuthException ex)
            {
                var firebaseEx = JsonConvert.DeserializeObject<FirebaseError>(ex.ResponseData);
                ModelState.AddModelError(String.Empty, firebaseEx.error.message);
                return View(loginModel);
            }

            return View();

        }


        public IActionResult SignIn()
        {
            return View();
        }

        //signin process
        [HttpPost]
        public async Task<IActionResult> SignIn(LogInModel loginModel)
        {
            try
            {
                //log in an existing user
                var fbAuthLink = await auth
                                .SignInWithEmailAndPasswordAsync(loginModel.Email, loginModel.Password);
                string token = fbAuthLink.FirebaseToken;
                //save the token to a session variable
                if (token != null)
                {

                    HttpContext.Session.SetString("_UserToken", token);

                    return RedirectToAction("Index");
                }

            }
            catch (FirebaseAuthException ex)
            {
                var firebaseEx = JsonConvert.DeserializeObject<FirebaseError>(ex.ResponseData);
                ModelState.AddModelError(String.Empty, firebaseEx.error.message);
                return View(loginModel);
            }

            return View();
        }

        //logout process
        public IActionResult LogOut()
        {
            HttpContext.Session.Remove("_UserToken");
            return RedirectToAction("SignIn");
        }



        /* -------------------------------------file uploading----------------------------------- */



        //Firebaseconfig

        IFirebaseConfig config = new FirebaseConfig
        {
            AuthSecret = "mf1yZW9FL1Yb34nb6GvHiuvrLcxdPVIoDxGq3rhW", //database secret
            BasePath = "https://polaroid-9bd32-default-rtdb.firebaseio.com/" //database url
        };
        IFirebaseClient client;
        public IActionResult UploadForm()
        {
            var token = HttpContext.Session.GetString("_UserToken");
            if (token != null)
            {
                ViewBag.sessionVerify = token;
                return View();
            }
            else
            {
                return RedirectToAction("Index");
            }

        }

        //uploading process


        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, UploadForm form)
        {

            var token = HttpContext.Session.GetString("_UserToken");

            if (file != null && file.Length > 0)
            {
                try
                {
                    //display message if successful
                    ViewBag.Message = "File Uploaded Successfully";

                    string webRootPath = _webHostEnvironment.WebRootPath;
                    //string contentRootPath = _webHostEnvironment.ContentRootPath;
                    string path = Path.Combine(webRootPath, "Images", file.FileName);
                    using (Stream fileStream = new FileStream(path, FileMode.Create))

                    {
                        await file.CopyToAsync(fileStream);
                    }
                    byte[] imageArray = System.IO.File.ReadAllBytes(path);
                    string ImageToBase64 = Convert.ToBase64String(imageArray); //convert image to byte to base64 for saving in db


                    client = new FireSharp.FirebaseClient(config);
                    var data = file.FileName;


                    form.FileName = file.FileName;
                    form.ImgtoBase64 = ImageToBase64;

                    PushResponse response = client.Push("Images/" + form.Id, form);

                    form.Id = response.Result.name;
                    SetResponse setResponse = client.Set("Images/" + form.Id, form);



                }
                catch (Exception ex)
                {
                    //display if error

                    ViewBag.Message = "Error: " + ex.Message.ToString();
                }

            }
            else
            {
                ViewBag.sessionVerify = token; //to maintain usertoken for navbar conditions.
                ViewBag.Message = "You have not specified a file";
            }
            ViewBag.sessionVerify = token;
            return RedirectToAction("Display");
        }


        //show uploaded item from cloud db

        [HttpGet]
        public IActionResult Display()
        {

            var token = HttpContext.Session.GetString("_UserToken");
            if (token != null)
            {
                ViewBag.sessionVerify = token;
                client = new FireSharp.FirebaseClient(config);
                FirebaseResponse response = client.Get("Images");
                dynamic data = JsonConvert.DeserializeObject<dynamic>(response.Body);
                var list = new List<UploadForm>();
                if (data != null)
                {
                    foreach (var item in data)
                    {
                        list.Add(JsonConvert.DeserializeObject<UploadForm>(((JProperty)item).Value.ToString()));
                    }

                }
                else
                {

                    return RedirectToAction("UploadForm");
                }
                return View(list);
            }
            else
            {
                return RedirectToAction("Index");
            }


        }

        //delete item
        public ActionResult Delete(string id)
        {

            client = new FireSharp.FirebaseClient(config);
            FirebaseResponse response = client.Delete("Images/" + id);
            System.Diagnostics.Debug.WriteLine(id);
            return RedirectToAction("Display");
        }




        //error handling
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {

            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}