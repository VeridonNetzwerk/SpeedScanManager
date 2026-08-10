var _____WB$wombat$assign$function_____=function(name){return (globalThis._wb_wombat && globalThis._wb_wombat.local_init && globalThis._wb_wombat.local_init(name))||globalThis[name];};if(!globalThis.__WB_pmw){globalThis.__WB_pmw=function(obj){this.__WB_source=obj;return this;}}{
let window = _____WB$wombat$assign$function_____("window");
let self = _____WB$wombat$assign$function_____("self");
let document = _____WB$wombat$assign$function_____("document");
let location = _____WB$wombat$assign$function_____("location");
let top = _____WB$wombat$assign$function_____("top");
let parent = _____WB$wombat$assign$function_____("parent");
let frames = _____WB$wombat$assign$function_____("frames");
let opener = _____WB$wombat$assign$function_____("opener");
<!--

	var ua = navigator.userAgent;
	var mode;

	if (ua.indexOf("Mac", 0) >= 0) { mode = "mac_"; }
	else if (ua.indexOf("Win", 0) >= 0) { mode = "win_"; }
	else { mode = "other_"; }

	if (ua.indexOf("Opera", 0) >= 0) { mode += "opera"; }
	else if (ua.indexOf("MSIE 8", 0) >= 0) { mode += "msie"; }
	else if (ua.indexOf("MSIE 7", 0) >= 0) { mode += "msie"; }
	else if (ua.indexOf("MSIE 6", 0) >= 0) { mode += "msie6"; }
	else if (ua.indexOf("Mozilla/4.0", 0) >= 0) { mode += "msie"; }
	else if (ua.indexOf("Mozilla/4", 0) >= 0) { mode += "ns4"; }
	else if (ua.indexOf("Firefox/3", 0) >= 0) { mode += "fs3"; }
	else if (ua.indexOf("Safari", 0) >= 0) { mode += "safari"; }
	else { mode += "other"; }
	
	var j$ = jQuery;
	
	switch (mode) {
		case "win_msie6": 
				document.write('<script type="text/javascript" src="https://web.archive.org/web/20110518212139/http://www.fujitsu.com/incv4/common/pngfix/DD_belatedPNG.js"></script>');
				document.write('<script type="text/javascript" src="https://web.archive.org/web/20110518212139/http://www.fujitsu.com/incv4/common/pngfix/pngfix.js"></script>');
				document.write('<script type="text/javascript" src="https://web.archive.org/web/20110518212139/http://www.fujitsu.com/incv4/common/libraries/boxrounded.js"></script>');
				break;
		case "win_msie": 
				document.write('<script type="text/javascript" src="https://web.archive.org/web/20110518212139/http://www.fujitsu.com/incv4/common/libraries/boxrounded.js"></script>');
				break;
		case "win_opera": 
				document.write('<script type="text/javascript" src="https://web.archive.org/web/20110518212139/http://www.fujitsu.com/incv4/common/libraries/boxrounded.js"></script>');
				break;
		case "win_other": break;
		case "mac_fs3": break;
		case "mac_safari": break;
		case "mac_other": break;
		case "mac_opera": break;
		case "mac_msie5": break;
		case "win_ns4": break;
		case "mac_ns4": break;
		default: document.write("<style type='text/css'></style>\n");
	}

	var cssbool = false;
	function csschk(){
		if(j$("div#corporatesymbol").css("float") == "left"){
			cssbool = true;
		}else{
			cssbool = false;
		}
		return cssbool;
	}


	j$(document).ready(function() {
		
		var BlockSkipNav = j$("#blockskip a");
		j$(BlockSkipNav).focus(function(){
			j$(this).addClass("show");
		});
		
		j$(BlockSkipNav).blur(function(){
			var scrHeight = j$(this).outerHeight({margin: true});
			j$(this).removeClass("show");
			if(!(ua.indexOf("Opera", 0) >= 0)){
				window.scrollBy(0,-scrHeight);
			}
		});
		
		
		j$("a.new-window").each(function(){
			j$(this).attr({target: '_blank'});
		});
		
		j$("div.stripe tbody tr:even").addClass("even");
		
		j$("p.pickupthumb + p").css("margin-top", "-10px");
		
		j$('li.bottom strong.current').wrap('<div style="background: transparent url(http://www.fujitsu.com/imgv4/common/mainmenu-li-last-current-bg.png) 1px bottom no-repeat;">');
		
		if(ua.indexOf("MSIE 6", 0) >= 0) {
			j$(".maincontents div.lay2col div.col1 > div.innerblock").css("margin", "0 8px 0 0");
			j$(".maincontents div.lay2col div.col2 > div.innerblock").css("margin", "0 1px 0 7px");
			j$(".maincontents div.lay3col div.col1 > div.innerblock").css("margin", "0 10px 0 0");
			j$(".maincontents div.lay3col div.col2 > div.innerblock").css("margin", "0 5px 0 5px");
			j$(".maincontents div.lay3col div.col3 > div.innerblock").css("margin", "0 0 0 10px");
			j$(".maincontents div.lay4col div.col1 > div.innerblock").css("margin", "0 12px 0 0");
			j$(".maincontents div.lay4col div.col2 > div.innerblock").css("margin", "0 9px 0 3px");
			j$(".maincontents div.lay4col div.col3 > div.innerblock").css("margin", "0 6px 0 6px");
			j$(".maincontents div.lay4col div.col4 > div.innerblock").css("margin", "0 3px 0 9px");
			j$(".maincontents div.lay5col div.col1 > div.innerblock").css("margin", "0 12px 0 0");
			j$(".maincontents div.lay5col div.col2 > div.innerblock").css("margin", "0 9px 0 3px");
			j$(".maincontents div.lay5col div.col3 > div.innerblock").css("margin", "0 6px 0 6px");
			j$(".maincontents div.lay5col div.col4 > div.innerblock").css("margin", "0 3px 0 9px");
			j$(".maincontents div.lay5col div.col5 > div.innerblock").css("margin", "0 0 0 12px");
			j$(".maincontents div.col1 > div.innerblock").css("padding", "0");
			j$(".maincontents div.col2 > div.innerblock").css("padding", "0");
			j$(".maincontents div.col3 > div.innerblock").css("padding", "0");
			j$(".maincontents div.col4 > div.innerblock").css("padding", "0");
			j$(".maincontents div.col5 > div.innerblock").css("padding", "0");
			j$(".maincontents div.frm > div.innerblock").css({padding: "15px 15px 0", margin: "0"});
			j$(".maincontents div.frm-grd > div.innerblock").css({padding: "14px 14px 0", margin: "0"});
			j$(".maincontents div.frm-bg > div.innerblock").css({padding: "14px 14px 0", margin: "0"});
			j$(".maincontents div.bg > div.innerblock").css({padding: "15px 15px 0", margin: "0"});
		}
	});
	
//-->
}

/*
     FILE ARCHIVED ON 21:21:39 May 18, 2011 AND RETRIEVED FROM THE
     INTERNET ARCHIVE ON 16:47:38 Aug 10, 2026.
     JAVASCRIPT APPENDED BY WAYBACK MACHINE, COPYRIGHT INTERNET ARCHIVE.

     ALL OTHER CONTENT MAY ALSO BE PROTECTED BY COPYRIGHT (17 U.S.C.
     SECTION 108(a)(3)).
*/
/*
playback timings (ms):
  capture_cache.get: 0.757
  captures_list: 1.073
  exclusion.robots: 0.118
  exclusion.robots.policy: 0.1
  esindex: 0.012
  cdx.remote: 5.623
  LoadShardBlock: 317.19 (6)
  PetaboxLoader3.datanode: 273.415 (7)
  PetaboxLoader3.resolve: 127.7 (2)
  load_resource: 120.373
*/