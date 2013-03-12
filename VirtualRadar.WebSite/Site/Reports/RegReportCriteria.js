function RegReportCriteria()
{
    var that = this.registerSubclass(this);

    var mRegistration = null;

    this.getRegistration = function() { return mRegistration; };
    this.setRegistration = function(value) { mRegistration = value; if(mRegistration !== null) mRegistration = mRegistration.toUpperCase(); };

    this.subclassCopyFromUI = function(form)
    {
        that.setRegistration(trim(form.critRegistration.value));
    };

    this.subclassCopyToUI = function(form)
    {
        form.critRegistration.value = mRegistration === null ? '' : mRegistration;
    };

    this.subclassIsValid = function()
    {
        return mRegistration !== null && mRegistration.length > 0;
    };

    this.subclassToString = function()
    {
        return '&reg=' + encodeURIComponent(mRegistration);
    };

    this.subclassAddUI = function()
    {
        return that.addUITextField('critRegistration', 'Registration', 10, true);
    };
}

RegReportCriteria.prototype = new ReportCriteria();