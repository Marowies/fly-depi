using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace SkyScan.Presentation.Services
{
    public interface ILanguageService
    {
        string CurrentLanguage { get; }
        bool IsRtl { get; }
        string T(string key);
        
    }

    public class LanguageService : ILanguageService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LanguageService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string CurrentLanguage
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                if (context != null)
                {
                    if (context.Request.Cookies.TryGetValue("Language", out var lang))
                    {
                        if (lang == "ar" || lang == "en")
                            return lang;
                    }
                }
                return "en"; // Default Language
            }
        }

        public bool IsRtl => CurrentLanguage == "ar";

        private static readonly Dictionary<string, string> ArabicTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Flights", "الرحلات" },
            { "Bookings", "حجوزاتي" },
            { "Profile", "الملف الشخصي" },
            { "Sign In", "تسجيل الدخول" },
            { "Sign Out", "تسجيل الخروج" },
            { "Select Currency", "اختر العملة" },
            { "Global Access", "وصول عالمي" },
            { "Every Destination, One Search.", "كل وجهة، في بحث واحد." },
            { "Fly Beyond", "حلق وراء" },
            { "Time & Sky", "الزمن والسماء" },
            { "Every destination deserves a seamless beginning. Compare flights, explore new horizons, and travel with comfort, confidence, and style.", "كل وجهة تستحق بداية سلسة. قارن بين الرحلات، واستكشف آفاقًا جديدة، وسافر براحة وثقة وأناقة." },
            { "Global Reach", "الوصول العالمي" },
            { "5,000+", "٥,٠٠٠+" },
            { "Airports Worldwide", "مطار حول العالم" },
            { "Search Flights", "البحث عن رحلات" },
            { "From", "من" },
            { "To", "إلى" },
            { "Origin City...", "مدينة المغادرة..." },
            { "Destination City...", "مدينة الوصول..." },
            { "Departure", "المغادرة" },
            { "Return", "العودة" },
            { "Cabin Class", "درجة المقصورة" },
            { "First Class", "الدرجة الأولى" },
            { "Business", "درجة رجال الأعمال" },
            { "Premium", "الدرجة السياحية الممتازة" },
            { "Economy", "الدرجة السياحية" },
            { "Economy Class", "الدرجة السياحية" },
            { "One Way", "ذهاب فقط" },
            { "Round Trip", "ذهاب وعودة" },
            { "Real-time Insights", "رؤى في الوقت الفعلي" },
            { "Trending Flight Routes", "مسارات الرحلات الشائعة" },
            { "The most popular journeys booked and searched by our members.", "الرحلات الأكثر شعبية التي تم حجزها والبحث عنها من قبل أعضائنا." },
            { "SkyScan Curated", "مجموعات سكاي سكان المختارة" },
            { "The Horizon Collection", "مجموعة الأفق" },
            { "View All Destinations", "عرض جميع الوجهات" },
            { "New Opening", "افتتاح جديد" },
            { "Tokyo Nocturne", "ليالي طوكيو" },
            { "Experience the neon-soaked luxury of the Ginza District's newest private lounge.", "عش تجربة الفخامة الغارقة في النيون في أحدث صالة خاصة في حي غينزا." },
            { "Alpine Sanctuary", "ملاذ جبال الألب" },
            { "Desert Mirage", "سراب الصحراء" },
            { "SkyScan On-Time Arrival", "وصول سكاي سكان في الوقت المحدد" },
            { "Global SkyScan Lounges", "صالات سكاي سكان العالمية" },
            { "SkyScan Concierge", "كونسيرج سكاي سكان" },
            { "Privacy Policy", "سياسة الخصوصية" },
            { "Terms of Service", "شروط الخدمة" },
            { "Luxury Partners", "الشركاء المميزون" },
            { "Support", "الدعم الفني" },
            { "Outbound Leg", "رحلة الذهاب" },
            { "Return Leg", "رحلة العودة" },
            { "Outbound Journey", "رحلة الذهاب" },
            { "Return Journey", "رحلة العودة" },
            { "Fastest Route", "أسرع مسار" },
            { "Non-stop", "بدون توقف" },
            { "Stop", "توقف" },
            { "Stops", "توقفات" },
            { "Included", "مشمول" },
            { "Favorite", "المفضلة" },
            { "Flight Details", "تفاصيل الرحلة" },
            { "Sort By", "ترتيب حسب" },
            { "Price", "السعر" },
            { "Duration", "المدة" },
            { "Filter Flights", "تصفية الرحلات" },
            { "Max Stops", "الحد الأقصى للتوقفات" },
            { "Airlines", "شركات الطيران" },
            { "Price Range", "نطاق السعر" },
            { "Reset Filters", "إعادة تعيين الفلاتر" },
            { "No flights found", "لم يتم العثور على رحلات" },
            { "Book Flight", "حجز الرحلة" },
            { "Total Price", "السعر الإجمالي" },
            { "Cancel", "إلغاء" },
            { "In-Flight Amenities", "الخدمات على متن الطائرة" },
            { "Free WiFi", "واي فاي مجاني" },
            { "Hot Meals", "وجبات ساخنة" },
            { "USB Outlets", "مخارج USB" },
            { "Entertainment", "وسائل ترفيه" },
            { "Baggage & Cabin", "الأمتعة والمقصورة" },
            { "Carry-on Baggage", "حقائب المقصورة" },
            { "Checked Baggage", "حقائب الشحن" },
            { "1x 7kg Included", "١ حقيبة ٧ كجم مشمولة" },
            { "1x 23kg Included", "١ حقيبة ٢٣ كجم مشمولة" },
            { "Up to 1 Stop", "حتى توقف واحد" },
            { "2+ Stops", "توقفين أو أكثر" },
            { "Non-stop Only", "بدون توقف فقط" },
            { "Refine Journey", "تعديل البحث" },
            { "Max Price", "السعر الأقصى" },
            { "Upgrade to Private Access", "الترقية للوصول الخاص" },
            { "Skip the terminals. Enjoy direct apron transfers and private lounge access across 40 global hubs.", "تجنب صالات الانتظار. استمتع بنقل مباشر من الساحة ودخول الصالات الخاصة في ٤٠ مركزًا عالميًا." },
            { "Learn More", "معرفة المزيد" },
            { "Select your path", "اختر مسارك" },
            { "Results Found", "نتائج تم العثور عليها" },
            { "No Paths Found", "لم يتم العثور على مسارات" },
            { "Your specific filters don't match any current itineraries. Adjust your parameters to explore other possibilities.", "الفلاتر المحددة لا تطابق أي رحلات حالية. يرجى تعديلها لاستكشاف خيارات أخرى." },
            { "Reset All Filters", "إعادة تعيين كل الفلاتر" },
            { "SkyScan Itinerary", "خط سير رحلة سكاي سكان" },
            { "Sign In to Get Started", "سجل الدخول للبدء" },
            { "Email Address", "البريد الإلكتروني" },
            { "Password", "كلمة المرور" },
            { "Confirm Password", "تأكيد كلمة المرور" },
            { "Remember Me", "تذكرني" },
            { "Forgot Password?", "هل نسيت كلمة المرور؟" },
            { "Login", "دخول" },
            { "Continue with Google", "المتابعة باستخدام جوجل" },
            { "Don't have an account yet?", "ليس لديك حساب بعد؟" },
            { "Apply for Access", "طلب العضوية" },
            { "Didn't receive confirmation email?", "لم تستلم بريد التأكيد الإلكتروني؟" },
            { "Resend it", "أعد إرساله" },
            { "Name", "الاسم" },
            { "Register Account", "تسجيل الحساب" },
            { "Already have an account?", "لديك حساب بالفعل؟" },
            { "VIP Access", "وصول كبار الشخصيات" },
            { "Join SkyScan", "انضم إلى سكاي سكان" },
            { "Create your account to unlock curated itineraries and concierge services.", "أنشئ حسابك لفتح خطوط السير المنظمة وخدمات الكونسيرج." },
            { "SkyScan Member", "عضو سكاي سكان" },
            { "Personal Details", "التفاصيل الشخصية" },
            { "Full Name", "الاسم الكامل" },
            { "Security & Authenticator", "الأمان والمصادقة ثنائية العامل" },
            { "Enhance your SkyScan profile security by enabling Two-Factor Authentication (2FA).", "عزز أمان ملفك الشخصي في سكاي سكان عن طريق تفعيل المصادقة ثنائية العامل." },
            { "2FA Status", "حالة المصادقة الثنائية" },
            { "Enabled", "مفعل" },
            { "Disabled", "معطل" },
            { "Disable Two-Factor Auth", "تعطيل المصادقة الثنائية" },
            { "Set Up Two-Factor Auth", "إعداد المصادقة الثنائية" },
            { "Reset Password", "إعادة تعيين كلمة المرور" },
            { "Change your login password regularly for safety.", "قم بتغيير كلمة المرور بشكل دوري للحفاظ على الأمان." },
            { "Current Password", "كلمة المرور الحالية" },
            { "New Password", "كلمة المرور الجديدة" },
            { "Confirm New Password", "تأكيد كلمة المرور الجديدة" },
            { "Update Password", "تحديث كلمة المرور" },
            { "Monitored Routes & Favorites", "المسارات المتابعة والمفضلة" },
            { "Your Search History", "سجل البحث الخاص بك" },
            { "No recent flight searches recorded.", "لم يتم تسجيل أي عمليات بحث حديثة." },
            { "Date Searched", "تاريخ البحث" },
            { "Route", "المسار" },
            { "Departure Date", "تاريخ المغادرة" },
            { "Trip Type", "نوع الرحلة" },
            { "Search Again", "ابحث مجدداً" },
            { "Saved", "محفوظة" },
            { "No routes saved to favorites yet.", "لم يتم حفظ أي مسارات في المفضلة بعد." },
            { "Favorite flight paths to monitor price changes and save schedules here.", "أضف مسارات رحلاتك المفضلة لمتابعة تغيرات الأسعار وحفظ المواعيد هنا." },
            { "Disclaimer: Prices shown are estimates and may not be final. Please verify details before booking.", "تنويه: الأسعار المعروضة هي أسعار تقديرية وقد لا تكون نهائية. يرجى التحقق من التفاصيل قبل تأكيد الحجز." },
            { "SkyScan Collection", "مجموعة سكاي سكان" },
            { "Your Booked Journeys", "رحلاتك المحجوزة" },
            { "A curation of your scheduled skies and historical flights.", "مجموعة من رحلاتك المجدولة والسابقة." },
            { "No Bookings Yet", "لا توجد حجوزات بعد" },
            { "You haven't reserved any flights yet. Let us search and prepare your next itinerary.", "لم تقم بحجز أي رحلات بعد. دعنا نبحث ونجهز لك رحلتك القادمة." },
            { "Start Exploring", "ابدأ الاستكشاف" },
            { "Booked", "تم الحجز" },
            { "View Ticket", "عرض التذكرة" },
            { "Remove from favorites", "إزالة من المفضلة" },
            { "Target Price", "السعر المستهدف" },
            { "Monitoring Until", "مراقبة حتى" },
            { "Track and send emails of this booking to price alert.", "تتبع وإرسال رسائل بريد إلكتروني بهذا الحجز إلى تنبيه الأسعار." },
            { "Add this booking to my calendar.", "إضافة هذا الحجز إلى التقويم الخاص بي." }
        };

       

        public string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (CurrentLanguage == "ar")
            {
                if (ArabicTranslations.TryGetValue(key, out var translated))
                {
                    return translated;
                }
            }
            return key; // Default to English
        }

        
    }
}
