# AdventureWorks Travel Intelligent App Deployment
## Part of the Cortana Intelligence Suite Workshop

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://azuredeploy.net/)

This GitHub repo exists to deploy the web application that is part of the *Cortana Intelligence Suite Workshop*. We are leveraging a capability of Azure called ARM templates which allow you to specifiy what your solution looks like from a deployment perspetive simply by using JSON code. This is a fairly simple use of ARM templates, but you can actually deploy very complex topologies using this technology - straight from source control. Pretty cool!!

You will need to have completed the requisite exercises in order to deploy this web application to your Azure subscription. You will also need to have created your free developer account at http://www.wunderground.com/weather/api/ and retrieved your developer API key.

Once you have gathered the following information, you are ready to click the "Delpoy to Azure" button at the top/bottom of this page.

* Your Azure ML web service API key
* Your Azure ML workspace ID
* Your Azure ML service ID
* Your [Weather Underground API key](http://www.wunderground.com/weather/api/)

After clicking the button, you will see a screen where you will need to provide the above information as well as information needed to by Azure to deploy such as:

* The target subscription
* The target resource group (choose existing or create a new one)
* The name of your website (needs to be globally unique)
* Where your site should be deployed

After the web app deployment is completed, you should be able to navigate to the URL provided and test your finished product. Congratulations!

![alt text](/images/webapp.png "Azure Deployment GUI")

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://azuredeploy.net/)
