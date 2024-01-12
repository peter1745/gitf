workspace "gitf"
	configurations { "Debug", "Release" }

project "gitf"
	kind "ConsoleApp"
	language "C#"
	dotnetframework "net8.0"

	location "gitf/"

	nuget {
		"Microsoft.TemplateEngine.Utils:8.0.101"
	}

	files {
		"gitf/Source/**.cs"
	}