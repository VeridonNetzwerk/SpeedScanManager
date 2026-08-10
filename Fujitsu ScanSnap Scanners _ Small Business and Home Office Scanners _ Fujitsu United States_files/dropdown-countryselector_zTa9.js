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
// DropDown in Country Selector
//-------------------------------------------
<!--
function Activate_countryselector_action() {
	var setDropDownCS = function (j$target) {
		
		var ua = navigator.userAgent;
	
		var j$list = j$target.find("li");
		
		j$target.css("display", "block");
		var menu_single_width = j$target.width();
		j$target.css("display", "none");
		
		
		//__________ initialize __________
		
		if ((ua.indexOf("Win", 0) >= 0) && ((ua.indexOf("MSIE", 0) >= 0))) {
			j$target.parent(".maincontents").css("position","relative");
		}
		
		j$list.each(function() {
			if(j$(this).is(":has(ul)")) {
				j$(this).addClass("has_children");
				j$(this).children("ul").css("width",menu_single_width + "px");
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
					if(ua.indexOf("Opera", 0) >= 0){
						distance_right = window_width - ((j$("div.dd-country-selector p.lang").offset().left - 8 - j$(window).scrollLeft()) + menu_single_width*2);
					};
					
					j$(this).css("position","relative");
					j$child_node.css("position","absolute");
					
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
						j$child_node.css("left", (menu_single_width - 8) + "px");
					} else {
						j$child_node.css("right","100%");
					}
				}
				
				j$(this).addClass("selected");
				j$(this).children("a").addClass("selected");
				j$(this).children("span").addClass("selected");
			},
			function() {
				j$child_node = j$(this).find("> ul");
				j$(this).find("> ul").css("position","static");
				j$(this).css("position","static");
				j$(this).removeClass("selected");
				j$(this).children("a").removeClass("selected");
				j$(this).children("span").removeClass("selected");
				j$child_node.css("left", "auto");
				j$child_node.css("right", "auto");
				j$child_node.css("position","static");
			}
		);
	}
	
	if(csschk() == true){
		$(document).ready(function () {
			
			var top_wrapper = "div.header";
			var top_wrapper_default_z_index = "1000";
			var wrapper = "div.headertop";
			var wrapper_default_z_index = "0";
			//if(!(ua.indexOf("MSIE 8",0) >= 0)){
			//	j$("p.lang").after("<br />");
			//}
			if(ua.indexOf("Firefox",0) >= 0){
				j$("#headnavi .dd-country-selector").css("display","inline-block");
			}
			
			var j$target_nodes = j$("div.dd-country-selector");
			j$target_nodes.hover( // This method work pre-setup only. etDropDownCS is main function.
				function() {
					j$(this).css("position","relative");
					j$(this).children("ul.dropdown-box").css({
						position : "absolute",
						display : "block"
					});
					j$(wrapper).css({
						position : "relative", 
						zIndex : "2"
					});
					if(ua.indexOf("Opera", 0) >= 0){
						var opera_offset_L = j$(window).scrollLeft();
						var opera_offset_T = j$(window).scrollTop();
						j$(this).children("ul.dropdown-box").css({
							marginLeft : opera_offset_L + "px",
							marginTop : opera_offset_T + "px"
						});
					}
					
				},
				function() {
					j$(this).css("position","static");
					j$(this).children("ul.dropdown-box").css({
						display : "none",
						position : "static"
					});
					
					j$(wrapper).css({
						position : "static",
						zIndex : wrapper_default_z_index
					});
				}
			);
			j$target_nodes.each(function() {
				setDropDownCS(j$(this).children("ul.dropdown-box"));
			});
		});
	}
}
//-->

}

/*
     FILE ARCHIVED ON 23:23:06 May 20, 2011 AND RETRIEVED FROM THE
     INTERNET ARCHIVE ON 16:47:44 Aug 10, 2026.
     JAVASCRIPT APPENDED BY WAYBACK MACHINE, COPYRIGHT INTERNET ARCHIVE.

     ALL OTHER CONTENT MAY ALSO BE PROTECTED BY COPYRIGHT (17 U.S.C.
     SECTION 108(a)(3)).
*/
/*
playback timings (ms):
  capture_cache.get: 0.311
  captures_list: 0.375
  exclusion.robots: 0.046
  exclusion.robots.policy: 0.038
  esindex: 0.007
  cdx.remote: 4.307
  LoadShardBlock: 44.845 (3)
  PetaboxLoader3.datanode: 80.285 (5)
  load_resource: 142.109 (2)
  PetaboxLoader3.resolve: 67.342 (2)
*/