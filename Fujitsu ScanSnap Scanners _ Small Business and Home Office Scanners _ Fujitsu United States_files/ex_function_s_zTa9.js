var _____WB$wombat$assign$function_____=function(name){return (globalThis._wb_wombat && globalThis._wb_wombat.local_init && globalThis._wb_wombat.local_init(name))||globalThis[name];};if(!globalThis.__WB_pmw){globalThis.__WB_pmw=function(obj){this.__WB_source=obj;return this;}}{
let window = _____WB$wombat$assign$function_____("window");
let self = _____WB$wombat$assign$function_____("self");
let document = _____WB$wombat$assign$function_____("document");
let location = _____WB$wombat$assign$function_____("location");
let top = _____WB$wombat$assign$function_____("top");
let parent = _____WB$wombat$assign$function_____("parent");
let frames = _____WB$wombat$assign$function_____("frames");
let opener = _____WB$wombat$assign$function_____("opener");

var track_image=new Image;
track_image.src="https://web.archive.org/web/20110518212531/http://www.fujitsu.com/img/common/tracker.gif?h="+screen.availHeight+"&w="+screen.availWidth+"&userAgent="+escape(navigator.userAgent);


/* DROP DOWN */
function goThereDropDown(form, fieldname) {
    field = form[fieldname];
    if ( field == null ) { return false; }
    url = field.options[field.selectedIndex].value;
    if ( url.length > 0 ) {
        window.location.href = url;
        return true;
    }
    return false;
}

/* FLASH PLUGIN */
var plugver=0;
var flplugin = ((navigator.mimeTypes && navigator.mimeTypes["application/x-shockwave-flash"]) ? navigator.mimeTypes["application/x-shockwave-flash"].enabledPlugin : 0);

if(flplugin){
    var plugver=parseInt(navigator.mimeTypes["application/x-shockwave-flash"].enabledPlugin.description.substring(navigator.mimeTypes["application/x-shockwave-flash"].enabledPlugin.description.indexOf(".")-1));
}

function plugin_check() {
    document.write("<object classid='clsid:D27CDB6E-AE6D-11cf-96B8-444553540000' codebase='https://web.archive.org/web/20110518212531/http://download.macromedia.com/pub/shockwave/cabs/flash/swflash.cab#version=5,0,0,0' width="+f_width+" height="+f_height+">");
    document.write("<param name=movie value="+f_src+"><param name=quality value=high><param name=bgcolor value="+f_bgcolor+">");

    if(plugver>=ok_version || (navigator.userAgent.indexOf('Mac',0) >= 0 && navigator.appVersion.indexOf('MSIE 3.',0) >= 0) || (navigator.userAgent.indexOf('Mac',0) >= 0 && navigator.appVersion.indexOf('MSIE 4.',0) >= 0)){
        document.write("<embed src="+f_src+" quality=high pluginspage='https://web.archive.org/web/20110518212531/http://www.macromedia.com/jp/shockwave/download/index.cgi?P1_Prod_Version=ShockwaveFlash' type='application/x-shockwave-flash' width="+f_width+" height="+f_height+" alt="+f_alt+" bgcolor="+f_bgcolor+">");
        document.write("</embed>");
    }else{
        document.write(change_img);
    }
    document.write("</object>");
}

/* LEFT NAVI POPUP */
function popnavi(root,ver){
    var url = root + "navi/navi_" + ver + ".html";
    naviwin=window.open( url,"navi","toolbar=no,location=no,menubar=no,directories=no,status=no,scrollbars=no,resizable=no,top=0,left=0,width=280,height=520");
    naviwin.focus();
}

/* GENERIC POPUP */
function popup(pURL,pName,features){
    new_window = window.open(pURL, pName, features);
}

/* IMAGE POPUP */
function image_popup(pUrl,pAlt,pWidth,pHeight){
    var width = 620;
    var height = 460;
    if ( window.screen && (pWidth+30) < window.screen.availWidth ) {
        width = pWidth+30;
    }
    else if ( window.screen ) {
        width = window.screen.availWidth-30
    }
    if ( window.screen &&  (pHeight+60) < window.screen.availHeight ) {
        height = pHeight+60;
    }
    else if ( window.screen ) {
        height = window.screen.availHeight-30
    }
    popup(pUrl+'&alt='+escape(pAlt),'large_image', 'width='+width+',height='+height+',scrollbars=yes,toolbars=no,resizable=yes');
}

/* FBF2 css function */

var writeCSS=function(reg,ns) {
    var f =(((navigator.appName=="Netscape")&&(parseInt(navigator.appVersion)<=4))
    || (navigator.userAgent.indexOf("Opera",0)!=-1)) ? ns : reg;
    var inc ='<link rel="stylesheet" type="text/css" href="'+f+'">';
    document.open();
    document.write(inc);
    document.close();
}
var writeCss=writeCSS
function openLarge(src,w,h,desc,qt,swf, exfunctionSrc) {
    var aw=screen.availWidth;var ah=screen.availHeight;
    var sw=parseInt(w)+30;var sh=parseInt(h)+50;
    var big=(sw>aw)||(sh>ah);
    sw=(sw>aw)?aw:sw;sh=(sh>ah)?ah:sh;
    var html='<html><head><title>'+desc+'</title><script type="text/javascript" src="'+exfunctionSrc+'"></script></head><body  bgcolor="white" leftmargin="5" rightmargin="5" topmargin="5" marginwidth="5" marginheight="5" ><div align="center">';
    html+=qt?getQt(src,qt,w,h,desc):swf?getSwf(src,swf,w,h,desc):getImg(src,w,h,desc);
    html+='</div><form><div align="center"><input type="button" value="Close" onclick="window.close()" onkeypress="window.close()"></div></form></body></html>';
    var wstyle=((big)?'scrollbars=yes,':'')+'location=no,menubar=no,status=no,resizable=yes,toolbars=no,width='+sw+',height='+sh;
    var doc=window.open('','',wstyle).document;
    doc.open();
    doc.write(html);
    doc.close();
    return false;
}

function getImg(src,w,h,desc) {
	return '<img border="0" src="'+src+'" width="'+w+'" height="'+h+'" alt="'+desc+'">';
}

function getSwf(isrc,src,w,h,desc) {
	return '<div id="flash1"/><div id="flash_alt" style="display:none;">'+getImg(isrc,w,h,desc)+'</div><script defer="true" type="text/javascript">var so=new SWFObject("'+src+'","flashobject","flash_alt","'+w+'","'+h+'"); so.write("flash1");</script>';
}

function getQt(isrc,src,w,h,desc) {
	var htmlstr = '<div id="qt_alt" style="display:none;">'+getImg(isrc,w,h,desc)+'</div><script defer="true" type="text/javascript">var so=new QTObject("'+src+'","qtobject","qt_alt","'+w+'","'+h+'"); so.write();</script>';
return htmlstr;
}

function openWindow(src,w,h) {
    window.open(src, '', 'width=' + w + ',height=' + h + ',scrollbars=yes,location=no,menubar=no,status=no,resizable=yes,toolbars=no');
}

function goDropDown(f) {
	var s=f.elements[0].options[f.elements[0].selectedIndex].value;
	if(s&&s!='') window.location.href=s;
	return false;
}

/* FBF Forms validator class */

function FF_Validator(name) {
	this.fv=new Array;
	this.form=document.getElementById(name);
}

function FF_Validator_addFV(name,label,validators) {
	var f={name:name,label:label,validators:new Array};
	var _v=validators.split(',');
	for(var i=0; i<_v.length; ++i) if(_v[i]) f.validators[f.validators.length]=FV[_v[i]];
	this.fv[this.fv.length]=f;
}

function FF_Validator_validate() {
	var err='';
	for(var i=0;i<this.fv.length;++i) 
		for(var j=0;j<this.fv[i].validators.length;++j) {
			var e=this.fv[i].validators[j](this.form.elements[this.fv[i].name]);
			if(e) { err+=e.replace('%1',this.fv[i].label)+'\n'; break; }
		}
	if(err) alert(err);
	return !err;
}

FF_Validator.prototype.addFV=FF_Validator_addFV;
FF_Validator.prototype.validate=FF_Validator_validate;

/* FBF Forms validator class */

function FF_Validator(name) {
	this.fv=new Array;
	this.fc=new Array;
	this.form=document.forms[name];
}

function FF_Validator_addFV(name,label,validators,cond) {
	var f={name:name,label:label,validators:new Array,cond:cond};
	var _v=validators.split(',');
	for(var i=0; i<_v.length; ++i) if(_v[i]) f.validators[f.validators.length]=FV[_v[i]];
	this.fv[this.fv.length]=f;
}

function FF_Validator_addFC(fc) {
	fc.form=this.form;
	fc.position=0;
	for(var i=0;i<this.fc.length;++i) if(fc.name==this.fc[i].name) ++fc.position;
	this.fc[this.fc.length]=fc;
}

function FF_Validator_validate() {
	var err='';
	for(var i=0;i<this.fv.length;++i) 
		for(var j=0;j<this.fv[i].validators.length;++j) {
			var e=(!this.fv[i].cond||this.fv[i].cond.check(this.form))?this.fv[i].validators[j](this.form.elements[this.fv[i].name]):'';
			/* label disappears when field is not visible */
			if(e&&this.fv[i].label) { err+=e.replace('%1',this.fv[i].label)+'\n'; break; }
		}
	if(err) alert(err);
	return !err;
}

function FF_Validator_resetAll() {
	for(var i=0;i<this.fc.length;++i) this.fc[i].toggle(this.form);
	return true;
}

FF_Validator.prototype.addFV=FF_Validator_addFV;
FF_Validator.prototype.addFC=FF_Validator_addFC;
FF_Validator.prototype.validate=FF_Validator_validate;
FF_Validator.prototype.resetAll=FF_Validator_resetAll;

/* Condition evaluators */
function FC_Condition(fn,name,value,hide,fields) {
	eval("this.cond=FCC_"+fn);
	this.name=name;
	this.value=value;
	this.hide=hide;
	this.fields=fields.split(',');
}

function FC_Condition_check(f) {
	return this.cond(f.elements[this.name],this.value);
}

function FC_Condition_toggle() {
	var c=this.check(this.form);
	for(var i=0;i<this.fields.length;++i) FC_Condition_setEnabled(this.form.elements[this.fields[i]],c);
	if(this.hide) {
		var n="fl_"+(this.form.id?this.form.id:this.form.name)+"."+this.name+(this.position>0?this.position:"");
		var l=document.getElementById?document.getElementById(n):
		document.all?document.all[n]:'';
		if(l) l.style.display=c?"":"none";
	}
	return 1;
}

function FC_Condition_setEnabled(f,c) {
	if(f) switch(f.type||f[0].type) {
		case 'text':
		case 'textarea':
		case 'password':
		case 'select-multiple':
		case 'select-one':
			return f.disabled=!c;
		case 'radio':
		case 'checkbox':
			if(f.length) for(var i=0; i<f.length; ++i) f[i].disabled=!c;
			else f.disabled=!c;
	}
	return 1;
}

FC_Condition.prototype.check=FC_Condition_check;
FC_Condition.prototype.toggle=FC_Condition_toggle;

function FCC_is_non_empty(f) {
	switch(f.type||f[0].type) {
		case 'text':
		case 'textarea':
		case 'password':
			return f.value&&f.value!='';
		case 'select-multiple':
		case 'select-one':
			return f.selectedIndex&&f.selectedIndex>0;
		case 'radio':
		case 'checkbox':
		    if(f.checked) return true;
			if(f.length) { for(var i=0; i<f.length; ++i) if(f[i].checked) return true; }
			return f.disabled;
	}
	return false;
}

function FCC_is_empty(f) {
	return !FCC_is_non_empty(f);
}

function FCC_has_the_value(f,value) {
	switch(f.type||f[0].type) {
		case 'text':
		case 'textarea':
		case 'password':
			return f.value&&f.value==value;
		case 'select-multiple':
		case 'select-one':
		case 'radio':
		case 'checkbox':
			if(f.length) for(var i=0; i<f.length; ++i) if(f[i].checked&&f[i].value==value) return true;
			return f.value==value;
	}
	return false;
}

/* Field validators 
   Return the error message, or '' if okay */
function FV_required(f) {
	return FCC_is_non_empty(f)?'':_RB['f_required'];
}
function FV_email(f) {
	var p;
	return (!f.value||/^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$/.test(f.value))?'':_RB['f_e-mail'];
}
function FV_uszip(f) {
	return (!f.value||(f.value.length==5&&/\d{5}/.test(f.value))
	||(f.value.length==10&&/\d{5}-\d{4}/.test(f.value)))?'':_RB['f_uszip'];
}
function FV_usphone(f) {
	f.value=f.value.replace(/\s/g,'');
	return (!f.value||/1?[(-]?\d{3}[)-]\d{3}-?\d{4}/.test(f.value))?'':_RB['f_usphone'];
}
var FV=new Array;
FV['required']=FV_required;
FV['email']=FV_email;
FV['us-zip']=FV_uszip;
FV['us-phone']=FV_usphone;
var _RB=new Array;
var FBF_Locale='en';


/* TOGGLE OVERVIEW */
var plusIcon = new Image(14,14);
plusIcon .src = "https://web.archive.org/web/20110518212531/http://www.fujitsu.com/img/common/icons/toggle-plus.gif";
var minusIcon = new Image(14,14);
minusIcon.src = "https://web.archive.org/web/20110518212531/http://www.fujitsu.com/img/common/icons/toggle-minus.gif";


function ToggleOverview(overviewId, iconId) {
	var image=document.images?document.images[iconId]:'';
	var overview=
		document.getElementById?document.getElementById(overviewId).style:
		document.all?document.all[overviewId].style:'';
	if(image&&overview) {
		var s=(overview.display=="none");
		image.src=s?minusIcon.src:plusIcon.src;
		overview.display=s?"":"none";
		return false;
	} else return true;
}

/* R3.57 */
function Global() {
	this.h=new Array;
}
function Global_handleOnload() {
	for(var i=0;i<this.h.length;++i) { this.h[i](); }
}
function Global_addOnload(f) {
	this.h[this.h.length]=f;
}
Global.prototype.handleOnload=Global_handleOnload;
Global.prototype.addOnload=Global_addOnload;
var global=new Global();

window.onload=function() { global.handleOnload(); };

function ca(a,b) {
	var c=new Array();
	if(a.length) for(var i=0;i<a.length;++i) { c[c.length]=a[i]; }
	if(b.length) for(var i=0;i<b.length;++i) { c[c.length]=b[i]; }
	return c;
}

global.addOnload(function (){
	var rs=7; /* for spacing between toggle image and the following text */
	var ul=document.getElementsByTagName('ul');
	ul=ca(ul,document.getElementsByTagName('ol'));
	ul=ca(ul,document.getElementsByTagName('dl'));
	for(var i=0;i<ul.length;++i) {
		if(/(toggle)/.test(ul[i].className)) {
			var li=ul[i].getElementsByTagName('li');
			li=ca(li,ul[i].getElementsByTagName('dd'));
			for(var j=0;j<li.length;++j){
				var l=li[j];
				var a=l.getElementsByTagName('a')[0];
				if(!a) a=l.getElementsByTagName('h3')[0];
				var p=l.getElementsByTagName('div')[0];
				if(!p) p=l.getElementsByTagName('p')[0];				
				if(a) { 
					var a2=document.createElement('a');
					if(a.href) a2.href=a.href;
					var img=document.createElement('img');
					img.className = 'bordernone';	
					img.src = plusIcon.src;
					img.style.marginRight = rs+'px';
					a2.appendChild(img);
					a2.style.marginLeft='-'+(img.width+rs+(document.all?11:0))+'px';
					l.insertBefore(a2, l.firstChild);
					l.style.listStyle='none';
					if(p) p.style.display='none';
					a2.onclick=a2.onkeypress=function() {
						return toggle(this);			
					}
				}
			}
		}
	}
});

function toggle(a) {
	var i=a.getElementsByTagName('img')[0];
	var p=a.parentNode.getElementsByTagName('div')[0];
	if(!p) p=a.parentNode.getElementsByTagName('p')[0];
	if(!p) return true;
	if(i) {
		var s=(p.style.display=='none');
		i.src=s?minusIcon.src:plusIcon.src;
		p.style.display=s?"":"none";
		return false;
	} else return true;
}


/* TOGGLE ASSETINFO */
var infoIcon = new Image(16,16);
infoIcon .src = "https://web.archive.org/web/20110518212531/http://www.fujitsu.com/img/common/icons/info.gif";
var rinfoIcon = new Image(16,16);
rinfoIcon.src = "https://web.archive.org/web/20110518212531/http://www.fujitsu.com/img/common/icons/r-info.gif";


function ToggleAssetInfo(assetinfoId, iconId) {
	var image=document.images?document.images[iconId]:'';
	var assetinfo=
		document.getElementById?document.getElementById(assetinfoId).style:
		document.all?document.all[assetinfoId].style:'';
	if(image&&assetinfo) {
		var s=(assetinfo.display=="none");
		image.src=s?rinfoIcon.src:infoIcon.src;
		assetinfo.display=s?"":"none";
		return false;
	} else return true;
}






// TABS: Support Methods

// Extracts the tab set id from a tab id
function tabSetIdFromTabId(tabId) {
    if(tabId == null) { return null; }
    var idx = tabId.indexOf("/");
    if( idx <= 0 ) { return null; }
    return tabId.substr(0,idx);
}

// converts a content tab to a tab id
function tabIdFromContentId(tabContentId) {
    if(!tabContentId) { return null; }
    var content = "/Content";
    var idx = tabContentId.indexOf(content);
    return tabContentId.substr(0,idx) + tabContentId.substr(idx+content.length);
}

// Safari will have a single slash in file:// urls in window.location.href but triple slash
// in a.href, so this function normalizes the slashes to two
function tabUrlCleanup(url) {
    if(!url || url == "") { return url; }
    var colonSlashIdx = url.indexOf(":/");
    if(colonSlashIdx > 10 || colonSlashIdx < 0) { return url; }
    var idx = colonSlashIdx + 2;
    while( url.charAt(idx) == '/' ) { idx++; }
    return url.substr(0,colonSlashIdx) + "://" + url.substr(idx);
}

// This takes two urls, and returns the reference from the target, IF the target
// url is relative to the base url.  Null is returned if target does not have #ref
// or is not relative to base
function tabExtractReference(base, target) {
    var idx;
    idx = base.indexOf("#");
    if(idx > 0) { base = base.substr(0,idx); }
    idx = target.indexOf("#");
    if (idx < 0) { return null;}
    else if(idx == 0) { return target; }
    if(tabUrlCleanup(target.substr(0,idx)) == tabUrlCleanup(base)) {
        return target.substr(idx+1);
    }
    return null;
}

// TABS: Main functional methods

// Sets the default tab by looking for the first content div in each tab set, and also
// trying to extract the tab reference from the url
function tabShowDefaults() {
    if (!document.getElementsByTagName) { return null; }
    if (!document.getElementById) { return null; }
    // associative array of tabSetId, defaultTabId
    var defaultTabs = new Array();
    var tabSet = document.getElementById("Tab0");
    for(var num = 1; tabSet !=null; tabSet = document.getElementById("Tab"+num++)) {
        var divs = tabSet.getElementsByTagName("div");
        // we're finding the first div of type tab.
        for(var i=0; i < divs.length; i++) {
            var div = divs[i];
            var className = div.className;
            if (i == 0) {
                // apply a key style only if we have javascript
                div.className = "tabs";
            } 
            else if (className == "tabc" || className == "tabh") {
                defaultTabs[tabSet.id] = tabIdFromContentId(div.id);
                break;
            }
        }
    }
    // extract the default tab id from the url
    var urlTabId = tabExtractReference(window.location.href, window.location.href);
    var urlTabSetId = tabSetIdFromTabId(urlTabId)
    if(document.getElementById(urlTabId) && document.getElementById(urlTabSetId)) {
        defaultTabs[urlTabSetId] = urlTabId;
    }
    // show defaults
    for( tabSetId in defaultTabs ) {
        tabShow(defaultTabs[tabSetId]);
    }
    return true;
}

// Shows a tab's content div
function tabShowContent(tabId)
{
    if (!document.getElementsByTagName) { return null; }
    if (!document.getElementById) { return null; }
    // first we get the tabSet (the div that enloses both the tabs and content)
    var tabSet = document.getElementById(tabSetIdFromTabId(tabId));
    if(!tabSet) { return null; }
    // calc the content div from the tabId
    var tabContentId = tabSet.id + "/Content" + tabId.substr(tabSet.id.length);
    // now get all the divs in the tabSet
    var divs = tabSet.getElementsByTagName("div");
    for(var i=0; i < divs.length; i++) {
        var div = divs[i];
        var className = div.className;
        var id = div.id;
        if (id == tabContentId) {
            div.className = "tabc";
        } else if (className == "tabc") {
            div.className = "tabh";
        }
    }
}

// Sets a tab to current
function tabActivate(tabId) {
    if (!document.getElementsByTagName) { return null; }
    if (!document.getElementById) { return null; }
    // first we verify this is a valid tab
    var tab = document.getElementById(tabId);
    if(!tab) { return null; }
    // then we get the tabSet (the div that enloses both the tabs and content)
    var tabSet = document.getElementById(tabSetIdFromTabId(tabId));
    if(!tabSet) { return null; }
    // then all lis from the ul that contains the tabs
    var tabs = tabSet.getElementsByTagName("ul")[0].getElementsByTagName("li");
    for(var i = 0; i < tabs.length; i++) {
        tabs[i].className = "tab";
    }
    tab.className = "current";
}

// Adds javascript onclick commands to tab links
function tabFixLinks()
{
    if (!document.getElementsByTagName) { return null; }
    if (!document.getElementById) { return null; }
    var tabSet = document.getElementById("Tab0");
    for(var num = 1; tabSet != null; tabSet = document.getElementById("Tab"+num++)) {
        var tabUL = tabSet.getElementsByTagName("ul")[0];
        var anchors = tabUL.getElementsByTagName("a");
        for(var i=0; i < anchors.length; i++) {
            var a = anchors[i];
            var ref = tabExtractReference(window.location.href, a.href);
            if (ref != null) {
                ref = "tabShow('" + ref + "');";
                a.setAttribute("href", "javascript: " + ref);
                a.setAttribute("onclick",ref+';this.blur()');
                a.setAttribute("onkeypress",ref+';this.blur()');
            }
        }      
    }
    
    return true;
}

// TABS: External methods

// Main method to show a specific tab and hide all others in the set
function tabShow(tabId) {
    tabActivate(tabId)
    tabShowContent(tabId);
}

// initialize tabs -- should be called by onload
global.addOnload(function () {
    tabFixLinks();
    tabShowDefaults();
    
    return true;
});

/* The following code is a function for generating the <object> contents for
 * FLASH.  This is necessary for the eolas patent where the <object> tag cannot
 * be written directly into the HTML
 *
 * Code was adapted from the following:
 *
 * SWFObject v1.4.2: Flash Player detection and embed - http://blog.deconcept.com/swfobject/
 *
 * SWFObject is (c) 2006 Geoff Stearns and is released under the MIT License:
 * http://www.opensource.org/licenses/mit-license.php
 *
 */
/** 
 * Changes from the orignal were made:
 *  (1) changed variable names for clarity
 *  (2) do not change the onload function to clean up 
 *  (3) do not require a flashVersion parameter -- just use 8 as a major version
 *	(4) require an altId for a <div> with alternate content
 *  (5) always set skipDetect to true and do install prompting
 */
if(typeof deconcept=="undefined"){var deconcept=new Object();}
if(typeof deconcept.util=="undefined"){deconcept.util=new Object();}
if(typeof deconcept.SWFObjectUtil=="undefined"){deconcept.SWFObjectUtil=new Object();}
deconcept.SWFObject=function(swfURL,id, altId, w,h,flashVersion,bgcolor,useExpInstall,quality,_9,_a,_b){
if(!document.getElementById){return;}
this.DETECT_KEY=_b?_b:"detectflash";
//this.skipDetect=deconcept.util.getRequestParameter(this.DETECT_KEY);
this.skipDetect=true;
this.params=new Object();
this.variables=new Object();
this.attributes=new Array();
this.setAttribute("altId", altId);
if(swfURL){this.setAttribute("swf",swfURL);}
if(id){this.setAttribute("id",id);}
if(w){this.setAttribute("width",w);}
if(h){this.setAttribute("height",h);}
var requiredVersion = flashVersion ? flashVersion: "8";
this.setAttribute("version",new deconcept.PlayerVersion(requiredVersion.toString().split(".")));
this.installedVer=deconcept.SWFObjectUtil.getPlayerVersion();
if(bgcolor){this.addParam("bgcolor",bgcolor);}
var q=quality?quality:"high";
this.addParam("quality",q);
this.setAttribute("useExpressInstall",useExpInstall);
this.setAttribute("doExpressInstall",false);
var _d=(_9)?_9:window.location;
this.setAttribute("xiRedirectUrl",_d);
this.setAttribute("redirectUrl","");
if(_a){this.setAttribute("redirectUrl",_a);}};

deconcept.SWFObject.prototype={setAttribute:function(_e,_f){
this.attributes[_e]=_f;
},getAttribute:function(_10){
return this.attributes[_10];
},addParam:function(_11,_12){
this.params[_11]=_12;
},getParams:function(){
return this.params;
},addVariable:function(_13,_14){
this.variables[_13]=_14;
},getVariable:function(_15){
return this.variables[_15];
},getVariables:function(){
return this.variables;
},getVariablePairs:function(){
var _16=new Array();
var key;
var _18=this.getVariables();
for(key in _18){_16.push(key+"="+_18[key]);}
return _16;
},getSWFHTML:function(){
var _19="";
if(navigator.plugins&&navigator.mimeTypes&&navigator.mimeTypes.length){
if(this.getAttribute("doExpressInstall")){this.addVariable("MMplayerType","PlugIn");}
_19="<embed type=\"application/x-shockwave-flash\" src=\""+this.getAttribute("swf")+"\" width=\""+this.getAttribute("width")+"\" height=\""+this.getAttribute("height")+"\"";
_19+=" id=\""+this.getAttribute("id")+"\" name=\""+this.getAttribute("id")+"\" wmode=\"opaque\" ";
var _1a=this.getParams();
for(var key in _1a){_19+=key+"=\""+_1a[key]+"\" ";}
var _1c=this.getVariablePairs().join("&");
if(_1c.length>0){_19+="flashvars=\""+_1c+"\"";}
_19+="/>";
}else{if(this.getAttribute("doExpressInstall")){
this.addVariable("MMplayerType","ActiveX");}
_19="<object id=\""+this.getAttribute("id")+"\" classid=\"clsid:D27CDB6E-AE6D-11cf-96B8-444553540000\" width=\""+this.getAttribute("width")+"\" height=\""+this.getAttribute("height")+"\" codebase=\"http://download.macromedia.com/pub/shockwave/cabs/flash/swflash.cab#version="+this.getAttribute("version").major+",0,0,0\">";
_19+="<param name=\"movie\" value=\""+this.getAttribute("swf")+"\" />";
var _1d=this.getParams();
for(var key in _1d){_19+="<param name=\""+key+"\" value=\""+_1d[key]+"\" />";}
var _1f=this.getVariablePairs().join("&");
if(_1f.length>0){_19+="<param name=\"flashvars\" value=\""+_1f+"\" />";}
var altElem = document.getElementById(this.getAttribute("altId"));
_19+=altElem.innerHTML+ "<param name=\"wmode\" value=\"opaque\" /></object>";}
return _19;
},write:function(_20){
if(this.getAttribute("useExpressInstall")){
var _21=new deconcept.PlayerVersion([6,0,65]);
if(this.installedVer.versionIsValid(_21)&&!this.installedVer.versionIsValid(this.getAttribute("version"))){
this.setAttribute("doExpressInstall",true);
this.addVariable("MMredirectURL",escape(this.getAttribute("xiRedirectUrl")));
document.title=document.title.slice(0,47)+" - Flash Player Installation";
this.addVariable("MMdoctitle",document.title);}}
if(this.skipDetect||this.getAttribute("doExpressInstall")||this.installedVer.versionIsValid(this.getAttribute("version")))
{
var n=(typeof _20=="string")?document.getElementById(_20):_20;
n.innerHTML=this.getSWFHTML();
return true;
}else{
if(this.getAttribute("redirectUrl")!=""){document.location.replace(this.getAttribute("redirectUrl"));}}
if (!this.installedVer.versionIsValid(this.getAttribute("version")) &&
   this.installedVer.flashIsInstalled())
{
var n=(typeof _20=="string")?document.getElementById(_20):_20;
n.innerHTML="You need to upgrade flash";
}

return false;}};


deconcept.SWFObjectUtil.getPlayerVersion=function(){
var _23=new deconcept.PlayerVersion([0,0,0]);
if(navigator.plugins&&navigator.mimeTypes.length){
var x=navigator.plugins["Shockwave Flash"];
if(x&&x.description){_23=new deconcept.PlayerVersion(x.description.replace(/([a-zA-Z]|\s)+/,"").replace(/(\s+r|\s+b[0-9]+)/,".").split("."));}
}else{
try{var axo=new ActiveXObject("ShockwaveFlash.ShockwaveFlash.7");}
catch(e){try{
var axo=new ActiveXObject("ShockwaveFlash.ShockwaveFlash.6");
_23=new deconcept.PlayerVersion([6,0,21]);
axo.AllowScriptAccess="always";}
catch(e){
if(_23.major==6){return _23;}}try{axo=new ActiveXObject("ShockwaveFlash.ShockwaveFlash");}
catch(e){}}
if(axo!=null){_23=new deconcept.PlayerVersion(axo.GetVariable("$version").split(" ")[1].split(","));}}
return _23;};
deconcept.PlayerVersion=function(_27){
this.major=_27[0]!=null?parseInt(_27[0]):0;
this.minor=_27[1]!=null?parseInt(_27[1]):0;
this.rev=_27[2]!=null?parseInt(_27[2]):0;
};
deconcept.PlayerVersion.prototype.versionIsValid=function(fver){
if(this.major<fver.major){return false;}
if(this.major>fver.major){return true;}
if(this.minor<fver.minor){return false;}
if(this.minor>fver.minor){return true;}
if(this.rev<fver.rev){return false;}
return true;
};
deconcept.PlayerVersion.prototype.flashIsInstalled=function(){
if (this.major == 0 && this.minor == 0 ) {return false;}
return true;
}
deconcept.PlayerVersion.prototype.getMajorVersion=function() {
return this.major;
}

deconcept.util={getRequestParameter:function(_29){
var q=document.location.search||document.location.hash;
if(q){
var _2b=q.substring(1).split("&");
for(var i=0;i<_2b.length;i++){
if(_2b[i].substring(0,_2b[i].indexOf("="))==_29){
return _2b[i].substring((_2b[i].indexOf("=")+1));}}}
return "";}};
deconcept.SWFObjectUtil.cleanupSWFs=function(){
var _2d=document.getElementsByTagName("OBJECT");
for(var i=0;i<_2d.length;i++){
_2d[i].style.display="none";
for(var x in _2d[i]){if(typeof _2d[i][x]=="function"){_2d[i][x]=null;}}}};
/*
if(typeof window.onunload=="function"){
var oldunload=window.onunload;
window.onunload=function(){
deconcept.SWFObjectUtil.cleanupSWFs();
oldunload();};
}else{window.onunload=deconcept.SWFObjectUtil.cleanupSWFs;}
*/
if(Array.prototype.push==null){
Array.prototype.push=function(_30){
this[this.length]=_30;
return this.length;};}

var getQueryParamValue=deconcept.util.getRequestParameter;
var FlashObject=deconcept.SWFObject; // for legacy support
var SWFObject=deconcept.SWFObject;


/*
 * The following is code to generate the <object> tag for Quicktime
 * It is similar to the SWFObject code above, and is done for the Eolas patent
 *
 * Code was adapted from the following:
 *
 * QTObject embed
 * http://blog.deconcept.com/2005/01/26/web-standards-compliant-javascript-quicktime-detect-and-embed/
 *
 * by Geoff Stearns (geoff@deconcept.com, http://www.deconcept.com/)
 * Embeds a quicktime movie to the page, includes plugin detection
 *
 * Usage:
 *
 *	myQTObject = new QTObject("path/to/mov.mov", "movid","altDivId", "width", "height");
 *	myQTObject.write();
 *
 * Changes were made to require a "altDivId" which contains the alternate content
 * This alternate content is included within the <object> for IE
 * Also, doDetect is set to FALSE to just use the browser prompting for install
 * instead of detecting the version and comparing 
 */



QTObject = function(mov,  id, altId, w, h) {
	this.mov = mov;
	this.id = id;
	this.width = w;
	this.height = h;
	this.redirect = "";
	this.altId = altId;
	this.sq = document.location.search.split("?")[1] || "";
// Note: code changes were made such that altTxt and bypassTxt are not used
	this.altTxt = "This content requires the QuickTime Plugin. <a href='https://web.archive.org/web/20110518212531/http://www.apple.com/quicktime/download/'>Download QuickTime Player</a>.";

	this.bypassTxt = "<p>Already have QuickTime Player? <a href='?detectqt=false&"+ this.sq +"'>Click here.</a></p>";

	this.params = new Object();
	//this.doDetect = getQueryParamValue('detectqt');
	this.doDetect = false;
}


QTObject.prototype.addParam = function(name, value) {

	this.params[name] = value;
}

QTObject.prototype.getParams = function() {

    return this.params;
}


QTObject.prototype.getParam = function(name) {

    return this.params[name];
}


QTObject.prototype.getParamTags = function() {
    var paramTags = "";
    for (var param in this.getParams()) {
        paramTags += '<param name="' + param + '" value="' + this.getParam(param) + '" />';
    }

    if (paramTags == "") {
        paramTags = null;
    }
    return paramTags;
}



QTObject.prototype.getHTML = function() {

    var qtHTML = "";

	if (navigator.plugins && navigator.plugins.length) { // not ie

        qtHTML += '<embed type="video/quicktime" src="' + this.mov + '" width="' + this.width + '" height="' + this.height + '" id="' + this.id + '"';

        for (var param in this.getParams()) {
            qtHTML += ' ' + param + '="' + this.getParam(param) + '"';
        }
        qtHTML += '></embed>';
    }

    else { // pc ie
        qtHTML += '<object classid="clsid:02BF25D5-8C17-4B23-BC80-D3488ABDDC6B" width="' + this.width + '" height="' + this.height + '" id="' + this.id + '" codebase="https://web.archive.org/web/20110518212531/http://www.apple.com/qtactivex/qtplugin.cab#version=6,0,2,0">';
        this.addParam("src", this.mov);
        if (this.getParamTags() != null) {
            qtHTML += this.getParamTags();
        }
		var altElem = document.getElementById(this.altId); 
        qtHTML += altElem.innerHTML + '</object>';
    }

    return qtHTML;
}

QTObject.prototype.write = function(elementId) {

// Note: changes were made such that doDetect is always false
	if(isQTInstalled() || !this.doDetect) {
		if (elementId) {
			document.getElementById(elementId).innerHTML = this.getHTML();
		} else {
			document.write(this.getHTML());
		}
	} else {
		if (this.redirect != "") {
			document.location.replace(this.redirect);
		} else {
			if (elementId) {
				document.getElementById(elementId).innerHTML = this.altTxt +""+ this.bypassTxt;

			} else {
				document.write(this.altTxt);
			}
		}

	}		

}

function isQTInstalled() {
	var qtInstalled = false;

	qtObj = false;

	if (navigator.plugins && navigator.plugins.length) {
		for (var i=0; i < navigator.plugins.length; i++ ) {
         var plugin = navigator.plugins[i];
         if (plugin.name.indexOf("QuickTime") > -1) {
			qtInstalled = true;
         }
      }
	} else {

		execScript('on error resume next: qtObj = IsObject(CreateObject("QuickTimeCheckObject.QuickTimeCheck.1"))','VBScript');

		qtInstalled = qtObj;

	}

	return qtInstalled;

}
/* get value of querystring param */

function getQueryParamValue(param) {

	var q = document.location.search;
	var detectIndex = q.indexOf(param);
	var endIndex = (q.indexOf("&", detectIndex) != -1) ? q.indexOf("&", detectIndex) : q.length;

	if(q.length > 1 && detectIndex != -1) {
		return q.substring(q.indexOf("=", detectIndex)+1, endIndex);
	} else {
		return "";
	}
}


/* 
  PARAMETRIC SEARC/BLOOM FILTERING 
*/

function checkFilter(listgroup, key) {
  var hash = hex_sha1(key);

  // extract the raw numbers 
  var p1 = parseInt(hash.substr(0,8),16);
  var p2 = parseInt(hash.substr(8,8),16);
  var p3 = parseInt(hash.substr(16,8),16);
  var p4 = parseInt(hash.substr(24,8),16);
  var p5 = parseInt(hash.substr(32,8),16);

  filter = filterArrays[listgroup];
  // convert to bit number
  p1 %= filter.length * 32;
  p2 %= filter.length * 32;
  p3 %= filter.length * 32;
  p4 %= filter.length * 32;
  p5 %= filter.length * 32;

  return checkFilterBit(listgroup, p1) && checkFilterBit(listgroup, p2) && checkFilterBit(listgroup, p3) && checkFilterBit(listgroup, p4) && checkFilterBit(listgroup, p5);
}

function checkFilterBit(listgroup, bitNumber) {
  var idx = Math.floor(bitNumber / 32);
  var bit = bitNumber % 32;
  var mask = 1 << bit;
  filterArr = filterArrays[listgroup];
  return((filterArr[idx] & mask) == mask);
}


listGroups = new Array();

filterArrays = new Array();

// Store the selection lists both by position and by name, so that they
// can be found easily for different purposes
function listGroup(name)
{
	this.listsByPos = new Array();
	this.name = name;
}

function selectListData(keyPosition)
{
	this.keyPosition = keyPosition;
	this.options = new Array();
	this.formSelectObject = null;
}


function addListGroup(name, filterArr)
{
	listGroups[name] = new listGroup(name);
	filterArrays[name] = filterArr;
}

// Associate a form <Select> object with a bloom filter list, and
// set up all the selection Option elementso
function assocFormSelect(formSelectElement, keyPos)
{
	lg = listGroups[formSelectElement.form.id];
	slist = new selectListData(keyPos);
	lg.listsByPos[keyPos] = slist;
	slist.formSelectObject = formSelectElement;
	for (i = 0; i < formSelectElement.options.length; i++)
	{
		slist.options[i] = formSelectElement.options[i];
	}
	formSelectElement.onchange=redoOptions;
}

// This function is called for onChange of a <Select> list 
// It is passed "this" for the element that has changed

function redoOptions()
{
	lg = listGroups[this.form.id];
	// first make an array of the current selections
	currentSelectObject = this;
	currentSelections = new Array(lg.listsByPos.length);
	for (i = 0; i < lg.listsByPos.length; i++)
	{
		selList = lg.listsByPos[i];
		if (selList != null)
		{
			formSelect = selList.formSelectObject;
			if (formSelect != null)
			{
				selectedVal = formSelect.options[formSelect.selectedIndex].value;
				currentSelections[i] = selectedVal;
			}
			// just do something here to avoid problems.  this case should
			// not occur with generated code
			else
				currentSelections[i] = "";
		}
	}
	// create an array to be used to hold a copy of the current selection,
	// but an entry will change in various positions.

	tempSel = new Array(currentSelections.length); 

	// run through each of the lists and the Select objects. If  it is not
	// the Select object that caused the onChange, we must redo all the
	// Option text and values by checking each option against the filter to 
	// see if the choice is valid
	for (pos = 0; pos < lg.listsByPos.length; pos++)
	{
		for (k = 0; k < currentSelections.length; k++)
			tempSel[k] = currentSelections[k];

		selList = lg.listsByPos[pos];
		formList = selList.formSelectObject;
		if (selList != null && formList != null && 
                 formList != currentSelectObject)
		{
			selOptions = selList.options;
			// ANY (the first option) will always be allowed for each list
			formList.options.length=0;
			formList.options[0] = selOptions[0];
			optionsIx = 1;
			for (k = 1; k < selOptions.length; k++)
			{
				tempSel[pos] =  selOptions[k].value;
				key = tempSel.join("|");
				res = checkFilter(this.form.id, key);
				if (res)
					formList.options[optionsIx++] = selOptions[k];
			}
			for (k = 0; k < formList.options.length; k++)
			{
				if (formList.options[k].value == currentSelections[pos])
					formList.selectedIndex = k;
			}
		}
	}
}



/*
 * A JavaScript implementation of the Secure Hash Algorithm, SHA-1, as defined
 * in FIPS PUB 180-1
 * Version 2.1a Copyright Paul Johnston 2000 - 2002.
 * Other contributors: Greg Holt, Andrew Kepert, Ydnar, Lostinet
 * Distributed under the BSD License
 * See http://pajhome.org.uk/crypt/md5 for details.
 */

/*
 * Configurable variables. You may need to tweak these to be compatible with
 * the server-side, but the defaults work in most cases.
 */
var hexcase = 0;  /* hex output format. 0 - lowercase; 1 - uppercase        */
var b64pad  = ""; /* base-64 pad character. "=" for strict RFC compliance   */
var chrsz   = 8;  /* bits per input character. 8 - ASCII; 16 - Unicode      */

/*
 * These are the functions you'll usually want to call
 * They take string arguments and return either hex or base-64 encoded strings
 */
function hex_sha1(s){return binb2hex(core_sha1(str2binb(s),s.length * chrsz));}
function b64_sha1(s){return binb2b64(core_sha1(str2binb(s),s.length * chrsz));}
function str_sha1(s){return binb2str(core_sha1(str2binb(s),s.length * chrsz));}
function hex_hmac_sha1(key, data){ return binb2hex(core_hmac_sha1(key, data));}
function b64_hmac_sha1(key, data){ return binb2b64(core_hmac_sha1(key, data));}
function str_hmac_sha1(key, data){ return binb2str(core_hmac_sha1(key, data));}

/*
 * Perform a simple self-test to see if the VM is working
 */
function sha1_vm_test()
{
  return hex_sha1("abc") == "a9993e364706816aba3e25717850c26c9cd0d89d";
}

/*
 * Calculate the SHA-1 of an array of big-endian words, and a bit length
 */
function core_sha1(x, len)
{
  /* append padding */
  x[len >> 5] |= 0x80 << (24 - len % 32);
  x[((len + 64 >> 9) << 4) + 15] = len;

  var w = Array(80);
  var a =  1732584193;
  var b = -271733879;
  var c = -1732584194;
  var d =  271733878;
  var e = -1009589776;

  for(var i = 0; i < x.length; i += 16)
  {
    var olda = a;
    var oldb = b;
    var oldc = c;
    var oldd = d;
    var olde = e;

    for(var j = 0; j < 80; j++)
    {
      if(j < 16) w[j] = x[i + j];
      else w[j] = rol(w[j-3] ^ w[j-8] ^ w[j-14] ^ w[j-16], 1);
      var t = safe_add(safe_add(rol(a, 5), sha1_ft(j, b, c, d)),
                       safe_add(safe_add(e, w[j]), sha1_kt(j)));
      e = d;
      d = c;
      c = rol(b, 30);
      b = a;
      a = t;
    }

    a = safe_add(a, olda);
    b = safe_add(b, oldb);
    c = safe_add(c, oldc);
    d = safe_add(d, oldd);
    e = safe_add(e, olde);
  }
  return Array(a, b, c, d, e);

}

/*
 * Perform the appropriate triplet combination function for the current
 * iteration
 */
function sha1_ft(t, b, c, d)
{
  if(t < 20) return (b & c) | ((~b) & d);
  if(t < 40) return b ^ c ^ d;
  if(t < 60) return (b & c) | (b & d) | (c & d);
  return b ^ c ^ d;
}

/*
 * Determine the appropriate additive constant for the current iteration
 */
function sha1_kt(t)
{
  return (t < 20) ?  1518500249 : (t < 40) ?  1859775393 :
         (t < 60) ? -1894007588 : -899497514;
}

/*
 * Calculate the HMAC-SHA1 of a key and some data
 */
function core_hmac_sha1(key, data)
{
  var bkey = str2binb(key);
  if(bkey.length > 16) bkey = core_sha1(bkey, key.length * chrsz);

  var ipad = Array(16), opad = Array(16);
  for(var i = 0; i < 16; i++)
  {
    ipad[i] = bkey[i] ^ 0x36363636;
    opad[i] = bkey[i] ^ 0x5C5C5C5C;
  }

  var hash = core_sha1(ipad.concat(str2binb(data)), 512 + data.length * chrsz);
  return core_sha1(opad.concat(hash), 512 + 160);
}

/*
 * Add integers, wrapping at 2^32. This uses 16-bit operations internally
 * to work around bugs in some JS interpreters.
 */
function safe_add(x, y)
{
  var lsw = (x & 0xFFFF) + (y & 0xFFFF);
  var msw = (x >> 16) + (y >> 16) + (lsw >> 16);
  return (msw << 16) | (lsw & 0xFFFF);
}

/*
 * Bitwise rotate a 32-bit number to the left.
 */
function rol(num, cnt)
{
  return (num << cnt) | (num >>> (32 - cnt));
}

/*
 * Convert an 8-bit or 16-bit string to an array of big-endian words
 * In 8-bit function, characters >255 have their hi-byte silently ignored.
 */
function str2binb(str)
{
  var bin = Array();
  var mask = (1 << chrsz) - 1;
  for(var i = 0; i < str.length * chrsz; i += chrsz)
    bin[i>>5] |= (str.charCodeAt(i / chrsz) & mask) << (32 - chrsz - i%32);
  return bin;
}

/*
 * Convert an array of big-endian words to a string
 */
function binb2str(bin)
{
  var str = "";
  var mask = (1 << chrsz) - 1;
  for(var i = 0; i < bin.length * 32; i += chrsz)
    str += String.fromCharCode((bin[i>>5] >>> (32 - chrsz - i%32)) & mask);
  return str;
}

/*
 * Convert an array of big-endian words to a hex string.
 */
function binb2hex(binarray)
{
  var hex_tab = hexcase ? "0123456789ABCDEF" : "0123456789abcdef";
  var str = "";
  for(var i = 0; i < binarray.length * 4; i++)
  {
    str += hex_tab.charAt((binarray[i>>2] >> ((3 - i%4)*8+4)) & 0xF) +
           hex_tab.charAt((binarray[i>>2] >> ((3 - i%4)*8  )) & 0xF);
  }
  return str;
}

/*
 * Convert an array of big-endian words to a base-64 string
 */
function binb2b64(binarray)
{
  var tab = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
  var str = "";
  for(var i = 0; i < binarray.length * 4; i += 3)
  {
    var triplet = (((binarray[i   >> 2] >> 8 * (3 -  i   %4)) & 0xFF) << 16)
                | (((binarray[i+1 >> 2] >> 8 * (3 - (i+1)%4)) & 0xFF) << 8 )
                |  ((binarray[i+2 >> 2] >> 8 * (3 - (i+2)%4)) & 0xFF);
    for(var j = 0; j < 4; j++)
    {
      if(i * 8 + j * 6 > binarray.length * 32) str += b64pad;
      else str += tab.charAt((triplet >> 6*(3-j)) & 0x3F);
    }
  }
  return str;
}






}

/*
     FILE ARCHIVED ON 21:25:31 May 18, 2011 AND RETRIEVED FROM THE
     INTERNET ARCHIVE ON 16:49:18 Aug 10, 2026.
     JAVASCRIPT APPENDED BY WAYBACK MACHINE, COPYRIGHT INTERNET ARCHIVE.

     ALL OTHER CONTENT MAY ALSO BE PROTECTED BY COPYRIGHT (17 U.S.C.
     SECTION 108(a)(3)).
*/
/*
playback timings (ms):
  capture_cache.get: 0.353
  captures_list: 0.365
  exclusion.robots: 0.04
  exclusion.robots.policy: 0.032
  esindex: 0.005
  cdx.remote: 4.909
  LoadShardBlock: 67.875 (3)
  PetaboxLoader3.resolve: 5.868
  PetaboxLoader3.datanode: 70.556 (4)
  load_resource: 26.316
*/