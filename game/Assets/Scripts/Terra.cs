/// <summary>
/// Реплика на теневом форуме «ТЕРРА» (страница браузера с ночи 2).
/// Посты и офферы живут на NightDirector и переживают ночь, но не переигрывание.
/// </summary>
public class ForumPost
{
    public string sender;
    public string text;
    public string time;

    public ForumPost() { }
    public ForumPost(string sender, string text, string time)
    {
        this.sender = sender;
        this.text = text;
        this.time = time;
    }
}

/// <summary>Оффер покупателя ТЕРРА: продать файл напрямую, минуя рынок.</summary>
public class TerraOffer
{
    public string buyer;
    public string file;    // пустая строка — «любой скачанный файл» (kuzmich_73)
    public int price;
    public bool requiresClean; // покупатель требует чистые логи и низкий след
    public bool done;

    public TerraOffer() { }
    public TerraOffer(string buyer, string file, int price, bool requiresClean)
    {
        this.buyer = buyer;
        this.file = file;
        this.price = price;
        this.requiresClean = requiresClean;
    }
}