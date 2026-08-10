var _____WB$wombat$assign$function_____=function(name){return (globalThis._wb_wombat && globalThis._wb_wombat.local_init && globalThis._wb_wombat.local_init(name))||globalThis[name];};if(!globalThis.__WB_pmw){globalThis.__WB_pmw=function(obj){this.__WB_source=obj;return this;}}{
let window = _____WB$wombat$assign$function_____("window");
let self = _____WB$wombat$assign$function_____("self");
let document = _____WB$wombat$assign$function_____("document");
let location = _____WB$wombat$assign$function_____("location");
let top = _____WB$wombat$assign$function_____("top");
let parent = _____WB$wombat$assign$function_____("parent");
let frames = _____WB$wombat$assign$function_____("frames");
let opener = _____WB$wombat$assign$function_____("opener");
//-------------------------------------------
// MegaDropDown & DropDown in MegaDrop
//-------------------------------------------

<!--

var Noopaque = false;
var setNoopaque = function(){
	Noopaque = true;
}


	function Activate_megadropdown_action(){
	
		var cssbool = false;
		var disp_timer,hide_timer;
		
		function noact(){
		};	
		var menuList = j$("#headerbottom li.glbnavlist");
		var hidemenuAll = j$("#headerbottom div.hidemenu");
		var mouseareaAll = j$("#headerbottom div.hidemenu_mousearea");
		var hidewrapperAll = j$(hidemenuAll).find("div.hidemenuwrapper");
		
		// add icon for dropdown
			j$(menuList).find("a span").css("background", "transparent url(http://www.fujitsu.com/imgv4/common/ajax/megadropdown/arrow-box-down-megadrop.gif) no-repeat right center");
			j$(menuList).find("a span").css("padding-right", "26px");
			
		// add roundedbox
			j$(hidewrapperAll).wrapInner('<div class="hidemenu-right-bottom clearfix"></div>');
			j$(hidewrapperAll).wrapInner('<div class="hidemenu-left-bottom"></div>');
			j$(hidewrapperAll).wrapInner('<div class="hidemenu-right-top"></div>');
			j$(hidewrapperAll).wrapInner('<div class="hidemenu-left-top"></div>');
		
		// change mouse cursor when mouseover to close button
		j$("#headerbottom div.hidemenuwrapper p.megaclosebtn").css("display", "block");
			j$("#headerbottom div.hidemenuwrapper p.megaclosebtn").find("img").css("cursor", "pointer");
		
		// copy title
		var megatitleAll = j$(menuList).find("a.glbnavtitle");
		
		// init
		for(var i=0; i<j$(megatitleAll).length; i++){
			var hidemenu = hidemenuAll[i];
			var mousearea = mouseareaAll[i];
			var megatitle = megatitleAll[i];
			
			if(csschk() == true){
				// copy title
					j$(megatitle).clone().prependTo(j$(mousearea));
					var copymegatitle = j$(hidemenu).find("a.glbnavtitle");
					// remove class
					j$(copymegatitle).removeClass("realtitle");
			}
		}
		
		function addMega(thisObject, focusTarget, callback){
			if(csschk() == true){
				
				if(focusTarget == null){
					
					for(k=0; k<j$(megatitleAll).length; k++){
						var thisMENU = j$("a.realtitle:eq("+k+")");
						if(j$(thisObject).find("a.realtitle").html() != j$(thisMENU).html()){
							j$("div.hidemenu:eq("+k+")").css("display", "none");
						}
					}
					
				}else{
					thisObject = focusTarget;
					var ACTIVEOBJ = j$(document.activeElement);
					for(k=0; k<j$(megatitleAll).length; k++){
						var thisMENU = j$("a.realtitle:eq("+k+")");
						if(j$(ACTIVEOBJ).html() != j$(thisMENU).html()){
							j$("div.hidemenu:eq("+k+")").css("display","none");
						}
					}
				}
				var hidemenu_one = j$(thisObject).find("div.hidemenu");
				var mousearea_one = j$(thisObject).find("div.hidemenu_mousearea");
				var megatitle_one = j$(thisObject).find("a.glbnavtitle");
				var megamenu_one = j$(thisObject).find("div.hidemenuwrapper");
				var copymegatitle_one = j$(hidemenu_one).find("a.glbnavtitle");
				
				// offest contents
					j$(hidemenu_one).css("display", "block");
						var hidewrapperWidth = j$(megamenu_one).width();
					j$(hidemenu_one).css("display", "none")
					var offsetL = j$(thisObject).offset().left - j$("div.bodyarea").offset().left;
					var my_area = (981 - offsetL);
					if (hidewrapperWidth > my_area) {
						var my_offset_r = (offsetL + hidewrapperWidth - 980);
						j$(mousearea_one).css("margin-left", - (my_offset_r -= 1) + "px");
						if(ua.indexOf("MSIE 7", 0) >= 0){
						}else if(ua.indexOf("MSIE 6", 0) >= 0){
						}else{
							j$(copymegatitle_one).css("margin-left", (my_offset_r -= 1) + "px");
						}
						
					} else {
						j$(megamenu_one).find("div.hidemenu-left-top").css("background", "transparent url(http://www.fujitsu.com/imgv4/common/ajax/megadropdown/dropdown-back-left-top-noround.png) no-repeat left top");
					}
				
				j$("#headerbottom").css("position", "relative");
				j$("#headsearch").parent().css({
					position : "relative",
					zIndex : "0"
				});
				j$(thisObject).parent().find("li.glbnavlist").css("position", "static");
				j$(thisObject).css("position", "relative");
				
				j$(hidemenu_one).css("display", "block");
				
				if(callback) {
					callback();
				}
			};
		};
		
		function removeMegaByMouseOut(thisObject, focusTarget){
			var status = j$(thisObject).css("position");
			var j$that;
			
			if (status === "relative") {
				j$that = j$(thisObject).find("div.hidemenu_mousearea").parent();
				j$that.parent().css("position", "static");
				j$("#headerbottom").css("position", "static");
				j$("#headsearch").parent().css({
					position : "static"
				});
				j$that.css("display", "none");
			} else {
				return;
			}
		};
		
		function removeMegaByClick(){
			var hidemenu = j$("div.hidemenu:visible");
			j$(hidemenu).css("display", "none");
		}
		
		function focusIn(event){
			var focusTarget = (j$(this).parent().get(0));
			addMega(this, focusTarget);
		};
		
		function deleteMenu(event){
			j$("div.hidemenu").css("display", "none");
		};
		
		if(csschk() == true){
			var j$glbnavlist = j$("#headerbottom").find("li.glbnavlist");
			var j$megawrraper = j$("div.hidemenuwrapper");
			
			// Global nav basic behavior
			j$glbnavlist.hover(
				function(){
					var that = this;
					
					clearTimeout(hide_timer);
					
					disp_timer = setTimeout(function(){
						addMega(that);
					},500);
				},
				function(e){
					var that = this;
					if(e.relatedTarget){
						var relTarg = e.relatedTarget || e.toElement;
						
						// limitation event object.
						if(!(relTarg.tagName.toUpperCase() === "SPAN" || j$(relTarg).hasClass("glbnavtitle"))) {
							clearTimeout(disp_timer);
							hide_timer = setTimeout(function(){
								removeMegaByMouseOut(that);
							},0);
						} else if (j$(relTarg).css("position") === "static") {
							removeMegaByMouseOut(that);
							clearTimeout(disp_timer);
						} else {
						}
					}else{
						removeMegaByMouseOut(that);
						clearTimeout(disp_timer);
					}
				}
			);
			
			/*If Mouse returned to Menu open area.
			j$megawrraper.mouseover(
				function(){
					clearTimeout(hide_timer);
				}
			)
			*/
			
			j$("#headerbottom p.megaclosebtn img").click(removeMegaByClick);
		}
		
		
		
		/* DropDown trigger */
		
		var setDropDown_inmegadrop = function (j$target) {
	
			var ua = navigator.userAgent;

			var j$list = j$target.find("li");
			
			j$("div.hidemenu").css("display", "block");
			var menu_single_width = j$target.width();
			j$("div.hidemenu").css("display", "none");
			
			//__________ initialize __________
			
			j$list.each(function() {
				j$(this).addClass("hidden");
				if(j$(this).is(":has(ul)")) {
					j$(this).addClass("has_children");
					j$(this).children("ul").css("width",menu_single_width);
				}
			});
			
			
			//__________ behavior __________
			
			j$list.hover(
				function () {
					var j$child_node;
					var distance_bottom;
					var distance_top;
					var distance_right;
					var window_width = j$(window).width();
					var window_height = j$(window).height();
					
					if(j$(this).children("ul").length) {
						var menu_total_width = menu_single_width*2 + j$(this).children("ul").width();
						
						j$child_node = j$(this).find("> ul");
						child_height = j$child_node.height() + 2; // "2" is twice the value of border-width.
						
						distance_bottom = window_height - ((j$(this).offset().top - j$(window).scrollTop()) + child_height);
						distance_bottom = Math.ceil(distance_bottom);
						
						distance_top = ((j$(this).outerHeight() + j$(this).offset().top) - j$(window).scrollTop()) - child_height;
						distance_top = Math.ceil(distance_top);
						
						distance_right = window_width - ((j$(this).offset().left - j$(window).scrollLeft()) + menu_single_width*2);
						distance_right = Math.ceil(distance_right);
						
						j$(this).css({
							position : "relative",
							zIndex : "1" // for IE
						});
						j$(this).removeClass("hidden");
						
						// If total height of menus is over window
						if (distance_bottom > 0) {
							j$child_node.css("top","0px");
						} else {
							if (distance_top > 0){
								j$child_node.css("top","-" + (child_height - j$(this).outerHeight() - 1) + "px");
							} else {
								j$child_node.css("top","0px");
							}
						}
						
						// If total wide of menus is over window
						if (distance_right > 0) {
							j$child_node.css("left","100%");
						} else {
							j$child_node.css("left","-100%");
						}
					}
					
					j$(this).addClass("selected");
					j$(this).children("a").addClass("selected");
					j$(this).children("span").addClass("selected");
				},
				function() {
					j$(this).css("position","static");
					j$(this).addClass("hidden");
					j$(this).removeClass("selected");
					j$(this).children("a").removeClass("selected");
					j$(this).children("span").removeClass("selected");
					
					if(!($(this).parent().parent().hasClass(".selected"))) {
					}
				}
			);
		}
		
		j$(document).ready(function() {
			
			if(Noopaque != true){
				if(j$("object").html() != null){
					j$("object").each(function(){
						j$(this).wrap("<div class='embedFlash' />");
					});
					
					j$("div.embedFlash").each(function(index) {
						var ObjTag = j$(this).find("object");
						var Objattr = new Object();
						var ParamAry = new Array();
						var ObjectSTR = new String();
						
						Objattr = {
							"ID":ObjTag.attr("id"),
							"NAME":ObjTag.attr("name"),
							"ALIGN":ObjTag.attr("align"),
							"CLASSID":ObjTag.attr("classid"),
							"CODEBASE":ObjTag.attr("codebase"),
							"HEIGHT":ObjTag.attr("height"),
							"WIDTH":ObjTag.attr("width"),
							"TYPE":ObjTag.attr("type"),
							"STYLE":ObjTag.attr("style")
						}
						
						for(var i=0; i < j$(this).find("param").length; i++ ){
							if(j$(this).find("param:eq("+i+")").attr("name") != "wmode"){
								ParamAry.push("<param name='"+j$(this).find("param:eq("+i+")").attr("name")+"' value='"+j$(this).find("param:eq("+i+")").attr("value")+"'>");
							}
						}
						
						ObjectSTR = "<object id='"+Objattr.ID+"' name='"+Objattr.NAME+"' align='"+Objattr.ALIGN+"' classid='"+Objattr.CLASSID+"' codebase='"+Objattr.CODEBASE+"' height='"+Objattr.HEIGHT+"' width='"+Objattr.WIDTH+"' type='"+Objattr.TYPE+"'>";
						
						for(var j=0; j < ParamAry.length; j++){
							ObjectSTR += ParamAry[j];
						}
						
						ObjectSTR += "<param name='wmode' value='opaque' /></object>";
						
						var EmbedTag = j$(this).find("embed");
						
						ObjTag.replaceWith(ObjectSTR);
						EmbedTag.attr("wmode", "opaque");
						j$(this).find("object").append(EmbedTag);
					});
				}else{			
					j$("embed").each(function(){
						j$(this).wrap("<div class='embedFlash' />");
					});
					
					j$("div.embedFlash").each(function(index) {
						var EmbedTag = j$(this).find("embed");
						EmbedTag.attr("wmode", "opaque");
						EmbedSTR = j$(this).html();
						EmbedTag.replaceWith(EmbedSTR);
					});
				}
			}
			
			j$("#headerbottom div.hidemenu div.hidemenuwrapper ul.dropdown-box").each(function() {
				setDropDown_inmegadrop(j$(this));
			});
		});

	}


//-->

}

/*
     FILE ARCHIVED ON 23:23:36 May 20, 2011 AND RETRIEVED FROM THE
     INTERNET ARCHIVE ON 16:47:45 Aug 10, 2026.
     JAVASCRIPT APPENDED BY WAYBACK MACHINE, COPYRIGHT INTERNET ARCHIVE.

     ALL OTHER CONTENT MAY ALSO BE PROTECTED BY COPYRIGHT (17 U.S.C.
     SECTION 108(a)(3)).
*/
/*
playback timings (ms):
  capture_cache.get: 0.386
  captures_list: 0.397
  exclusion.robots: 0.048
  exclusion.robots.policy: 0.04
  esindex: 0.005
  cdx.remote: 14.089
  LoadShardBlock: 85.159 (3)
  PetaboxLoader3.datanode: 238.029 (5)
  load_resource: 178.059 (2)
*/