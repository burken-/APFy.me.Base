
namespace APFy.me.utilities
{
    public enum ErrorCode
    {
        NoError = 0,
        ApiKeyMissing = 100,
        ApiKeyMalformed = 110,
        ApiKeyNonExisting = 120,
        ApiKeyRequestLimitReached = 130,
        UrlHttpsRequired = 200,
        UrlBadSyntax = 210,
        UrlApiNotFound = 220,
        RequestBadMethod = 300,
        RequestInvalidParameters = 310,
        RequestInvalidHeaders = 320,
        RequestFailed = 330,
        RequestBadResponse = 340,
        XsltLoadFail = 400,
        ParseFailed = 410,
        XsdLoadFail = 500,
        ValidationFailed = 510,
        UnknownError = 9000
    }
}