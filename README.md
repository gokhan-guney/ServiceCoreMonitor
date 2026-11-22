# ServiceCore Kayıt Bildirimi

**Geliştiren:** Gökhan Güney · **@gokhanguney**

ServiceCore'un **Çağrı (INT)**, **Olay (INC)** ve **İstek (SR)** listelerini Windows üzerinde izleyen küçük bir masaüstü uygulaması. Yeni bir kayıt numarası görüldüğünde bildirim gösterir. Kendi penceresinde Microsoft Edge WebView2 kullanır; tarayıcı eklentisi veya ServiceCore API erişimi gerekmez.

## Özellikler

- Üç kayıt türünü aynı anda, varsayılan olarak **10 saniyede bir** kontrol eder. Aralık 10–300 saniye arasında değiştirilebilir.
- Her tür için en yüksek sayısal kayıt numarasını ayrı saklar. Liste **“Veri Yok”** durumuna dönse bile son numarayı kaybetmez.
- Kurumun uygulama adresi açıldıktan sonra üç liste bağlantısını menüden bulmaya çalışır; bulunamazsa bilinen ServiceCore yollarını önerir. Adresler elle düzeltilebilir.
- **Bağlantıları hatırla** seçeneği, adreslerin sonraki açılışta geri gelmesini belirler.
- Hazır Windows sesleri, sessiz mod ve özel `.wav` bildirim sesi destekler.
- Pencere kapatıldığında sistem tepsisinde çalışmayı sürdürür. **Durum** sekmesi tarama sonuçlarını gösterir.

## Gereksinimler

- Windows 10/11 **x64**
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
- ServiceCore'a erişim ve geçerli kullanıcı hesabı; kurumunuz gerektiriyorsa VPN

## Kurulum ve ilk kullanım

1. **Releases** bölümünden ZIP paketini indirin ve kalıcı bir klasöre çıkarın. `ServiceCoreMonitor.exe` ile paketteki DLL dosyalarını aynı klasörde tutun.
2. VPN gerekiyorsa bağlanın, ardından `ServiceCoreMonitor.exe` dosyasını açın.
3. **Uygulama adresi** alanına kurumunuzun ServiceCore adresini yazıp **Sayfayı aç** düğmesine basın. **Giriş / Kayıt Aç** sekmesinde normal kullanıcı hesabınızla oturum açın.
4. Çağrı, Olay ve İstek sekmelerindeki **Liste adresi** alanları otomatik dolar. Kurumunuzda farklı yollar kullanılıyorsa bu alanları kendiniz düzenleyin.
5. Adreslerin sonraki açılışta saklanmasını istiyorsanız **Bağlantıları hatırla** kutusunu işaretleyin. İşaretlemezseniz adresleri yeniden girmeniz gerekir.
6. **İzlemeyi başlat** düğmesine basın. **Durum** sekmesinde her kategorinin tarama sonucunu ve son kayıt numarasını kontrol edin.

İlk başarılı taramada ekranda bulunan en yüksek numara **başlangıç** kabul edilir; eski kayıtlar bildirilmez. Liste ilk taramada boşsa başlangıç `0` olur ve sonra görünen ilk kayıt bildirilir. **Şimdi kontrol et** düğmesi beklemeden tarama yapar.

Pencereyi kapatmak uygulamayı sistem tepsisine indirir. Tamamen sonlandırmak için tepsi simgesine sağ tıklayıp **Çıkış** seçin.

## Bildirim sesi

**Bildirim sesi** menüsünden Windows seslerinden birini veya **Sessiz** seçeneğini seçebilirsiniz. **WAV seç** ile kendi `.wav` dosyanızı belirleyin; uygulama dosyayı ayar klasörüne kopyalar. **Sesi dene** sadece sesi, **Test bildirimi** ses ve uyarı penceresini sınar.

`notification.wav` dosyasını EXE'nin yanına koyarsanız uygulama ilk açılışta onu özel ses olarak otomatik seçer. Kaynak kod deposunda ve standart ZIP paketinde isteğe bağlı ses dosyası bulunmayabilir.

## Nasıl çalışır?

Uygulama ilgili sayfada görünen `INT`, `INC` veya `SR` ön ekli numaraların **sayısal kısmını** okur. Son kaydedilen sayıdan büyük bir numara görünürse bildirim verir ve eşiği günceller. “Veri Yok” yazısı görünürse eşik korunur. Aynı taramada birden fazla yeni numara varsa toplam sayı ve en büyük numara gösterilir.

İki tarama arasında gelip kaybolan kayıtlar saptanamaz. Ayrıca yalnızca hesabınızın görebildiği ve liste ekranında o anda görüntülenen kayıtlar kontrol edilir.

## Sorun giderme

| Belirti | Kontrol |
| --- | --- |
| Gömülü tarayıcı açılmıyor | WebView2 Runtime kurulu mu? Uygulama ve DLL dosyaları aynı klasörde mi? |
| Sayfa açılmıyor veya giriş ekranına dönüyor | VPN, kurum adresi ve oturumu kontrol edin; uygulamanın kendi **Giriş** sekmesinde yeniden oturum açın. |
| **Durum** sekmesinde numara bulunamadı | Liste bağlantısını kontrol edin. Kayıtların sayfada `INT`, `INC` veya `SR` numarasıyla görünmesi gerekir. |
| Test bildirimi çalışıyor ama gerçek kayıt bildirilmiyor | **Durum** sekmesindeki son numarayla yeni kaydın numarasını karşılaştırın. Yeni numara daha büyük olmalıdır. |
| Ses çıkmıyor | **Sesi dene** ile seçimi sınayın; Windows ses düzeyini ve seçili WAV dosyasını kontrol edin. |

## Yerel veriler ve bağlantı gizliliği

- Ayarlar ve son görülen numaralar: `%APPDATA%\ServiceCoreMonitor\state.json`
- Gömülü tarayıcının oturumu ve çerezleri: `%LOCALAPPDATA%\ServiceCoreMonitor\Browser`
- **Bağlantıları hatırla** kapalıyken girilen adresler `state.json` içinde saklanmaz. Tarayıcı oturumu ayrı tutulur ve bu seçenek onu silmez.
- Uygulama, kayıtları izlemenin dışında başka bir sunucuya veri göndermek için tasarlanmamıştır.

## Kaynak koddan derleme

Visual Studio'da **.NET desktop development** bileşeni ve .NET Framework 4.8 geliştirme araçlarıyla `ServiceCoreMonitor.csproj` dosyasını açın. NuGet geri yüklemesi `Microsoft.Web.WebView2` paketinin **1.0.3240.44** sürümünü indirir. **Release / x64** yapılandırmasıyla derleyin. Çalıştıracak bilgisayarda ayrıca WebView2 Runtime bulunmalıdır.

Kaynak deposunda `ServiceCoreMonitor.cs`, proje dosyası ve uygulama simgesi tutulur. Derlenmiş dosyaları dağıtmak için GitHub **Releases** bölümüne ZIP ekleyin. Kaynak kodu paylaşmadan önce projeye tercih ettiğiniz lisans dosyasını ekleyin.
