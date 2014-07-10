![Logo](http://www.apfy.me/content/fb_img.png)

Basic information
-----------------
APFy.me is a tool for transforming any website into a nicely formatted API. APFy.me consists of both a public website that you can use to define and consume API:s and a *proxy* that takes care of all necessary transformations. The proxy is released and available here on GitHub if you like your local installation, modify it or contribute in the development of the platform.

When you want to scrape data from a website or when you have your own site and want to provide an API to your users, APFy.me is a simple way to get started.

How it works
------------
Basically APFy.me works as a reverse proxy that relays your HTTP-requests and transforms and validates the response according to your needs before it goes back to you. Any call that you can make with a browser or by code you can also make through APF.me.

Instead of requesting a page directly you make the request through APFy.me and let the proxy do:
1. Transform the HTML into valid XML
2. Extract the exact data you need from the XML
3. Validate the data
4. Format the response as XMl or Json

E.g. You want to extract user rating for the movie "This Is Spinal Tap" from IMDB (http://www.imdb.com/title/tt0088258).
###Normal HTML-response###
When you view the source of this page or request it using e.g. CURL you will get quite a lot of HTML back. If we narrow it down to the information we're interested in it looks like this:
```html
[...]
<div class="star-box-details" itemtype="http://schema.org/AggregateRating" itemscope itemprop="aggregateRating">
            Ratings:
<strong><span itemprop="ratingValue">8,0</span></strong><span class="mellow">/<span itemprop="bestRating">11</span></span>            from <a href="ratings?ref_=tt_ov_rt" title="90 157 IMDb users have given a weighted average vote of 8/11" > <span itemprop="ratingCount">90 157</span> users
</a>&nbsp;
            Metascore: <a href="criticreviews?ref_=tt_ov_rt" title="85 review excerpts provided by Metacritic.com" > 85/100
</a>            <br/>
            Reviews:
<a href="reviews?ref_=tt_ov_rt" title="312 IMDb user reviews" > <span itemprop="reviewCount">312 user</span>
</a> 
                <span class="ghost">|</span>
<a href="externalreviews?ref_=tt_ov_rt" title="120 IMDb critic reviews" > <span itemprop="reviewCount">120 critic</span>
</a>             
                <span class="ghost">|</span>
<a href="criticreviews?ref_=tt_ov_rt" title="13 review excerpts provided by Metacritic.com" > 13
</a>                from
<a href="http://www.metacritic.com" target='_blank'> Metacritic.com
</a>             
         
    </div>
[...]
```

###Wouldn't you rather get it like this?###
After transforming the HTML to valid XML you can transform it using XSLT and XPath to extract and format the data as you like it. Your XSLT could look something like this:
```xml
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0"
	xmlns:regex="http://exslt.org/regular-expressions"
	xmlns:str="http://exslt.org/strings"
	exclude-result-prefixes="regex str">
<xsl:output method="xml" omit-xml-declaration="yes" />
	<xsl:template match="/">
	    <xsl:variable name="ratingBase" select="//*[@itemprop='aggregateRating']" />
	    <xsl:variable name="metaScore" select="regex:match($ratingBase//text()[regex:test(., '\bMetascore\b','i')]/following-sibling::a,'(\d+)/(\d+)')" />
		<movie>
		    <title><xsl:value-of select="//h1[@class='header']/*[@itemprop='name']" /></title>
		    <rating>
		        <score><xsl:value-of select="$ratingBase//*[@itemprop='ratingValue']" /></score>
		        <max><xsl:value-of select="$ratingBase//*[@itemprop='bestRating']" /></max>
		        <count><xsl:value-of select="regex:replace($ratingBase//*[@itemprop='ratingCount'],'[^\d]','g','')" /></count>
		    </rating>
		    <metascore>
		        <score><xsl:value-of select="$metaScore[2]" /></score>
		        <max><xsl:value-of select="$metaScore[3]" /></max>
		    </metascore>
		</movie>
	</xsl:template>
</xsl:stylesheet>
```

And now when calling http://apfy.me/burken/www.imdb.com/getMovieRating?id=tt0088258 the response would look like:
```xml
<movie>
    <title>Spinal Tap</title>
    <rating>
        <score>8.0</score>
        <max>11</max>
        <count>90157</count>
    </rating>
    <metascore>
        <score>85</score>
        <max>100</max>
    </metascore>
</movie>
```

**Or if you request Json**
```json
{
  "movie": {
    "title": "Spinal Tap",
    "rating": {
      "score": "8.0",
      "max": "11",
      "count": "90157"
    },
    "metascore": {
      "score": "85",
      "max": "100"
    }
  }
}
```

Setup
-----
If you like to install the proxy on your own server you need to define a database matching the Entity Framework model that is bundled in the project and of course modify the connection string to match your db. Apart from that you should be able to start using the code as is.

Links
-----
- [Homepage](http://www.apfy.me)
- [Playground](http://www.apfy.me/api/playground)
- [Getting started](http://www.apfy.me/page/getstarted)


