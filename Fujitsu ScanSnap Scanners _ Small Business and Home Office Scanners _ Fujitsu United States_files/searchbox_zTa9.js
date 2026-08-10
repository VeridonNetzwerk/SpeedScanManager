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
// SearchBox
//-------------------------------------------

<!--

if(config_searchbox) {
	alert("Javascript error occurd : global variable \"config_searchbox\" maybe overlaps other script");
} else {
	var config_searchbox = {
		id_textfield : "#Search",
		id_button : "#submit",
		input_status : false
	}
}

j$(document).ready(function() {

	var j$textfield = j$(config_searchbox.id_textfield);
	var j$button = j$(config_searchbox.id_button);
	var default_text = j$textfield.attr("title");
	
	var sfFocusIn = function (e) {
		var my_value = j$(this).attr("value");
		j$(this).css("color", "#333");
		if (my_value !== default_text) {
			return;
		} else {
			j$(this).attr("value","");
		}
	}
	
	var sfFocusOut = function (e) {
		var my_value = j$(this).attr("value");
		if (my_value === "" || my_value === default_text) {
			j$(this).css("color", "#777");
			j$(this).attr("value", default_text);
		} else {
			return;
		}
	}
	
	var sfEnter = function () {
		var my_value = j$textfield.attr("value");
		if (my_value === default_text) {
			j$textfield.attr("value","");
		} else {
			return;
		}
	}
	
	var sfReset = function () {
		j$textfield.attr("value",default_text)
	}
	
	j$textfield.focus(sfFocusIn);
	j$textfield.blur(sfFocusOut);
	
	j$button.click(sfEnter);
	
	j$(window).load(function(){
		sfReset();
	});
	
	 j$(window).keydown(function(e){
		var textfield_id = j$textfield.attr("id");
		
		if (e.keyCode === 13 && focus_elem_id === textfield_id) {
			sfEnter();
		}
	});
});



//-->

}

/*
     FILE ARCHIVED ON 23:22:45 May 20, 2011 AND RETRIEVED FROM THE
     INTERNET ARCHIVE ON 16:47:43 Aug 10, 2026.
     JAVASCRIPT APPENDED BY WAYBACK MACHINE, COPYRIGHT INTERNET ARCHIVE.

     ALL OTHER CONTENT MAY ALSO BE PROTECTED BY COPYRIGHT (17 U.S.C.
     SECTION 108(a)(3)).
*/
/*
playback timings (ms):
  capture_cache.get: 0.548
  captures_list: 0.578
  exclusion.robots: 0.085
  exclusion.robots.policy: 0.074
  esindex: 0.008
  cdx.remote: 6.168
  LoadShardBlock: 65.208 (3)
  PetaboxLoader3.datanode: 98.189 (5)
  load_resource: 158.566 (2)
  PetaboxLoader3.resolve: 79.685 (2)
*/