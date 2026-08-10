var _____WB$wombat$assign$function_____=function(name){return (globalThis._wb_wombat && globalThis._wb_wombat.local_init && globalThis._wb_wombat.local_init(name))||globalThis[name];};if(!globalThis.__WB_pmw){globalThis.__WB_pmw=function(obj){this.__WB_source=obj;return this;}}{
let window = _____WB$wombat$assign$function_____("window");
let self = _____WB$wombat$assign$function_____("self");
let document = _____WB$wombat$assign$function_____("document");
let location = _____WB$wombat$assign$function_____("location");
let top = _____WB$wombat$assign$function_____("top");
let parent = _____WB$wombat$assign$function_____("parent");
let frames = _____WB$wombat$assign$function_____("frames");
let opener = _____WB$wombat$assign$function_____("opener");

var etrigueSiteDomain = '.fujitsu.com';var etrigueLoc ="https://web.archive.org/web/20110710203525/https://login.etrigue.com";
var etrigueAID = 831;
var etrigueTrackStart = false;
var reg = get_cookie('etrigueReg');
var isMac,IEmac;isMac=(navigator.appVersion.indexOf('Mac')!=-1)?true:false;IEmac=((document.all)&&(isMac))?true:false;var estartTimeOnPage=new Date();var estopTime=1000*60*60;function SiteTrackeTrigue()
{var t='1';var moid='';var numPages='0';var exitTime='';var startTime='';var fromESP=false;var thisRefer=document.referrer; 
var deleteCookie = function( name, path, domain ) {if ( get_cookie( name ) ) document.cookie = name + "=" +( ( path ) ? ";path=" + path : "") +( ( domain ) ? "; domain=" + domain : "" ) +";expires=Thu, 01-Jan-1970 00:00:01 GMT";};
var thisTitle=document.title;if(thisTitle.length>40){thisTitle=thisTitle.substring(0,40);}var referChk=thisRefer.indexOf('fujitsu.com')!=-1
set_cookie('referrer',thisRefer,2020,01,15,'/','.fujitsu.com','');var myLocation=location.href;myCheck=myLocation.indexOf('gnikcartpse')!=-1; 
if(etrigueTrackStart || reg =='1'){myCheck = true; myLocation =get_cookie('moid');deleteCookie('etrigueReg', '/',etrigueSiteDomain );}if(myCheck)
{fromESP=myLocation.indexOf('0.4e')!=-1;set_cookie('moid',escape(myLocation),2020,01,15,'/','.fujitsu.com','');set_cookie('numPages',1,2020,01,15,'/','.fujitsu.com','');}
moid=get_cookie('moid');numPages=get_cookie('numPages');exitTime=get_cookie('ExitPageTime');startTime=get_cookie('StartPageTime');thisRefer=get_cookie('referrer');if(isNaN(numPages)==true||numPages==''||numPages==null||numPages=='NaN')
{numPages=1;set_cookie('numPages',numPages,2020,01,15,'/','.fujitsu.com','');var st=new Date();st=st.getTime();set_cookie('StartPageTime',st,2020,01,15,'/','.fujitsu.com','');}
else
{if(myCheck==false)
{numPages=parseInt(numPages)+1;set_cookie('numPages',numPages,2020,01,15,'/','.fujitsu.com','');}}
if(startTime!=null&&moid=='')
{var myStart=new Date();myStart=myStart.getTime();var sDif=parseInt(myStart)-parseInt(startTime);var sSec=sDif/(1000);if(parseInt(sSec)>=1200)
{numPages=1
set_cookie('numPages',numPages,2020,01,15,'/','.fujitsu.com','');}}
if(moid!=null)
{if(exitTime!=null)
{var myNow=new Date();myNow=myNow.getTime();myDifference=parseInt(myNow)-parseInt(exitTime);mySeconds=myDifference/(1000);if(parseInt(mySeconds)>=20)
{numPages=1
set_cookie('numPages',numPages,2020,01,15,'/','.fujitsu.com','');}}
var p=''+myLocation+'';var r=''+thisRefer+'';var n=''+(document.title)+'';if(n.length>40){n=n.substring(0,40);}
if(n==null||n=='')
{var query=(location.href);var mySplitCheck=query.indexOf('gnikcartpse')!=-1;if(mySplitCheck)
{var noQuery=query.substring(0,query.indexOf('gnikcartpse'));}
else
{var noQuery=query;}
n=''+noQuery+'';}
if(fromESP)
{var ranpg='https://web.archive.org/web/20110710203525/http://fujitsu.etrigue.com/cas/esp/connect.asp?'+t+';;;'+p+';;;'+r+';;;'+moid+';;;'+n+';;;'+numPages+'0.4eUpd';}
else
{var ranpg='https://web.archive.org/web/20110710203525/http://fujitsu.etrigue.com/cas/esp/connect.asp?'+t+';;;'+p+';;;'+r+';;;'+moid+';;;'+n+';;;'+numPages+';;;';}
esp_communication(ranpg);e=new Date();e=e.getTime();var newF='GetExitTime(0);';window.onunload=new Function(newF);window.close=new Function(newF);setTimeout('GetExitTime(2);',3000);}
else
{theunknown(thisRefer,numPages,myLocation,thisTitle);}}
function theunknown(referrer,numpages,location,title)
{var src='https://web.archive.org/web/20110710203525/http://fujitsu.etrigue.com/cas/esp/unknown.asp?'+referrer+';;;'+location+';;;'+numpages+';;;'+title;var I=new Image(1,1);I.src=src;}
function GetExitTime(visitorType)
{var frmCaptureString='';var moid=get_cookie('moid');var numPages=get_cookie('numPages');var captureFrmData=false;var nowTime=new Date();e=new Date();e=e.getTime();set_cookie('ExitPageTime',e,2020,01,15,'/','.fujitsu.com','');var myExit='https://web.archive.org/web/20110710203525/http://fujitsu.etrigue.com/cas/esp/connect.asp?'+moid+';;;'+numPages+'erutpacmrf';var II=new Image(2,2);II.src=myExit;if(nowTime.getTime() - estartTimeOnPage.getTime()  < estopTime)
{if(visitorType==2){setTimeout("GetExitTime(4);",9000);}
if(visitorType==4){setTimeout("GetExitTime(6);",11000);}
if(visitorType==6){setTimeout("GetExitTime(2);",15000);}}}
function get_cookie(Name)
{var cookies=' '+document.cookie;if(cookies.indexOf(' '+Name+'=')==-1)return null;var start=cookies.indexOf(' '+Name+'=')+(Name.length+2);var finish=cookies.substring(start,cookies.length);finish=(finish.indexOf(';')==-1)?cookies.length:start+finish.indexOf(';');return unescape(cookies.substring(start,finish));}
function esp_communication(locate)
{var I=new Image(2,2);I.src=locate;}
function set_cookie(name,value,exp_y,exp_m,exp_d,path,domain,secure)
{var cookie_string=name+'='+escape(value);if(exp_y)
{var expires=new Date(exp_y,exp_m,exp_d);cookie_string+='; expires='+expires.toGMTString();}
if(path)
cookie_string+='; path='+escape(path);if(domain)
cookie_string+='; domain='+escape(domain);if(secure)
cookie_string+='; secure';document.cookie=cookie_string;}
if(IEmac==false)
{SiteTrackeTrigue();}

 
	 
 	    
/*!
 
  Copyright(c) 2006-2011 eTrigue Corp. All rights Reserved
  eTrigue tracking script
 
 */(function(){var secure = "https:" == document.location.protocol; var tx=831;var path='/';var domain='.fujitsu.com'; etrigueDomain = domain; var searchTerms={"terms": [{"site": "daum","term": "q"},{"site": "eniro","term": "search_word"},{"site": "naver","term": "query"},{"site": "images.google","term": "q"},{"site": "google","term": "q"},{"site": "yahoo","term": "p"},{"site": "msn","term": "q"},{"site": "bing","term": "q"},{"site": "aol","term": "query"},{"site": "aol","term": "encquery"},{"site": "lycos","term": "query"},{"site": "ask","term": "q"},{"site": "altavista","term": "q"},{"site": "netscape","term": "query"},{"site": "cnn","term": "query"},{"site": "about","term": "terms"},{"site": "mamma","term": "query"},{"site": "alltheweb","term": "q"},{"site": "voila","term": "rdata"},{"site": "virgilio","term": "qs"},{"site": "live","term": "q"},{"site": "baidu","term": "wd"},{"site": "alice","term": "qs"},{"site": "yandex","term": "text"},{"site": "najdi","term": "q"},{"site": "aol","term": "q"},{"site": "mama","term": "query"},{"site": "seznam","term": "q"},{"site": "search","term": "q"},{"site": "wp","term": "szukaj"},{"site": "onet","term": "qt"},{"site": "szukacz","term": "q"},{"site": "yam","term": "k"},{"site": "pchome","term": "q"},{"site": "kvasir","term": "q"},{"site": "sesam","term": "q"},{"site": "ozu","term": "q"},{"site": "terra","term": "query"},{"site": "mynet","term": "q"},{"site": "ekolay","term": "q"},{"site": "rambler","term": "words"}]};searchTerms=searchTerms.terms;var errorCode=-99;var refreshCode=-98;var lookUpCode=-97;var intervalToSetCookie=1000*10;var timeTillSessionEnd=1000*20;var maxPingTime=1000*60*60;var pingTimer=1000*60*1;var expirationDays=3000;var setCookie=function(name,value,daysTillExpiration)
{var cookie_string=name+'='+escape(value);var expires=new Date();expires.setDate(expires.getDate()+daysTillExpiration);cookie_string+='; expires='+expires.toGMTString();if(path)
cookie_string+='; path='+escape('/');if(domain)
cookie_string+='; domain='+escape(domain);
if(secure) cookie_string += '; secure';
document.cookie=cookie_string;}
var deleteCookie=function(name){document.cookie=name+'=; expires=Thu, 01-Jan-70 00:00:01 GMT;';}
var deleteCookie2=function(name,path,domain){if(getCookie(name))document.cookie=name+"="+
((path)?";path="+path:"")+
((domain)?";domain="+domain:"")+";expires=Thu, 01-Jan-1970 00:00:01 GMT";}
var getCookie=function(c_name){if(document.cookie.length>0){c_start=document.cookie.indexOf(c_name+"=");if(c_start!=-1){c_start=c_start+c_name.length+1;c_end=document.cookie.indexOf(";",c_start);if(c_end==-1)c_end=document.cookie.length;return unescape(document.cookie.substring(c_start,c_end));}}
return"";};var head=document.getElementsByTagName("head")[0];var getScript=function(location){var script=document.createElement("script");script.src=location;script.type="text/javascript";head.appendChild(script);};var jsonpRequest=function(location,callBackName){location=location.replace('????',callBackName);getScript(location);};var pingPage=function(location){var src=location;var I=new Image(1,1);I.src=src;};var params=function(keyValues){var paramList="";for(var key in keyValues){paramList+=key+"="+keyValues[key]+"&";}
return paramList;};var getOS=function(){var OSName="Unknown";if(navigator.appVersion.indexOf("Win")!=-1)
OSName="Windows";else if(navigator.appVersion.indexOf("Mac")!=-1)
OSName="MacOS";else if(navigator.appVersion.indexOf("X11")!=-1)
OSName="UNIX";else if(navigator.appVersion.indexOf("Linux")!=-1)
OSName="Linux";else if(navigator.appVersion.indexOf("Chrome")!=-1)
OSName="Chrome";return OSName;};var updateTimeOnPage=function(){var now=new Date();var diff;now=now.getTime();diff=(now-startTime);if(diff<=maxPingTime){pingPage(restfulLink+params({cmd:"p",anonID:encodeURIComponent(anonymousID),anonVisitID:encodeURIComponent(anonVID),anonVisitDetailID:encodeURIComponent(vDetailID),t:tx,iv:encodeURIComponent(vector),domain:hostName}));setTimeout(updateTimeOnPage,pingTimer);}};var handlePageExit=function(){updateTimeOnPage();};var setExitCookie=function(){var now=new Date();var diff;now=now.getTime();diff=(now-startTime);if(diff<=maxPingTime){lastTimeHere=now;setCookie("etrigueAnonExit",now,expirationDays);setTimeout(setExitCookie,intervalToSetCookie);}};var isSameSession=function(){if(lastTimeHere=="")
return false;var now=new Date();var diff;now=now.getTime();diff=(now-lastTimeHere);if(diff<=timeTillSessionEnd)
return true;return false;};var clearAnonTracking2=function(){deleteCookie2("etrigueAnonID",path,domain);deleteCookie2("etrigueIV",path,domain);deleteCookie2("etrigueAnonVisitID",path,domain);deleteCookie2("etrigueAnonExit",path,domain);}
var getSearchTerms=function(url){if(url=="")return"";var sLength=searchTerms.length;var st;try{var i=0,h,k;if((i=url.indexOf("://"))<0)return"";h=url.substring(i+3,url.length);if(h.indexOf("/")>-1){h=h.substring(0,h.indexOf("/"));}
for(var idx=0;idx<sLength;idx++){st=searchTerms[idx];if(h.toLowerCase().indexOf(st.site.toLowerCase())>-1){if((i=url.indexOf("?"+st.term+"="))>-1||(i=url.indexOf("&"+st.term+"="))>-1){k=url.substring(i+st.term.length+2,url.length);if((i=k.indexOf("&"))>-1)
k=k.substring(0,i);}}}}catch(e){return"";}
return k;}
var initTracking=function(aID,sID){try{aID=aID||"NONE";anonVID=anonVID||"NONE";var command="NONE";var st="NONE";if(aID=="NONE"){command="c";st=getSearchTerms(document.referrer)||st;}
else if(!isSameSession()){command="cs";st=getSearchTerms(document.referrer)||st;}
else if(anonVID)
command="np";jsonpRequest(restfulLink+params({cmd:command,anonID:encodeURIComponent(aID),anonVisitID:encodeURIComponent(anonVID),iv:encodeURIComponent(vector),t:tx,os:operatingSystem,searchTerms:st,callBack:"????",domain:hostName,url:fullUrl,referrer:ref}),"etrigueTrackReady");}catch(e){}};var addUnLoadEvent=function(func){var oldonload=window.onunload;if(typeof window.onunload!='function'){window.onunload=func;}else{window.onunload=function(){if(oldonload){oldonload();}
func();}}};etrigueTrackReady=function(data){try{anonymousID=data.anonID;anonVID=data.anonVisitID;vDetailID=data.anonVisitDetailID;vector=data.iv;if(data.errorCode==errorCode||data.errorCode==lookUpCode||!anonymousID||!anonVID)
throw new Error("Server Error");else if(data.errorCode==refreshCode){clearAnonTracking2();}else{setCookie("etrigueAnonID",anonymousID,expirationDays);setCookie("etrigueIV",vector,expirationDays);setCookie("etrigueAnonVisitID",anonVID,expirationDays);setExitCookie();addUnLoadEvent(handlePageExit);setTimeout(updateTimeOnPage,pingTimer);}}catch(e){}}
etrigueOnFinish=function(data){try{if(data&&data.errorCode)
throw new Error("Server Error");else{clearAnonTracking2();handlePageExit=function(){};}}catch(e){}};var getVisitID=function(){var moid=getCookie("moid");var visitID="";try{if(moid=="")
return visitID;moid=moid.split('gnikcartpse')[0];visitID=moid.split('0.4eUpd')[1];}catch(e){visitID="";}
return visitID;};etrigueVisitID = getVisitID;try{var restfulLink="https://web.archive.org/web/20110710203525/https://login.etrigue.com/an/track.aspx?";var hostName=encodeURIComponent("http://"+window.location.hostname);var fullUrl=encodeURIComponent(window.location.href);var ref=encodeURIComponent(document.referrer)||"NONE";var operatingSystem=getOS();var anonymousID=getCookie("etrigueAnonID");var anonVID=getCookie("etrigueAnonVisitID");var vector=getCookie("etrigueIV");var lastTimeHere=getCookie("etrigueAnonExit");var vID=getVisitID();var vDetailID=-1;var startTime=new Date();etrigueMerge=function(visID){jsonpRequest(restfulLink+params({cmd:"m",visitID:getVisitID(),anonID:encodeURIComponent(anonymousID),t:tx,iv:encodeURIComponent(vector),domain:hostName,callBack:"????"}),"etrigueOnFinish");}
var etrigueAnonClear = function(){clearAnonTracking2();}; if(vID==""){initTracking(anonymousID);}else if(vID!=""&&anonymousID!=""){etrigueMerge();}else if(anonymousID!=""){etrigueAnonClear();}}catch(ex){}})(); 	    
 	    
 	 
}

/*
     FILE ARCHIVED ON 20:35:25 Jul 10, 2011 AND RETRIEVED FROM THE
     INTERNET ARCHIVE ON 00:03:13 Aug 10, 2026.
     JAVASCRIPT APPENDED BY WAYBACK MACHINE, COPYRIGHT INTERNET ARCHIVE.

     ALL OTHER CONTENT MAY ALSO BE PROTECTED BY COPYRIGHT (17 U.S.C.
     SECTION 108(a)(3)).
*/
/*
playback timings (ms):
  capture_cache.get: 0.364
  captures_list: 0.383
  exclusion.robots: 0.048
  exclusion.robots.policy: 0.041
  esindex: 0.006
  cdx.remote: 8.661
  LoadShardBlock: 119.418 (3)
  PetaboxLoader3.datanode: 109.8 (4)
  PetaboxLoader3.resolve: 84.58 (2)
  load_resource: 121.781
*/